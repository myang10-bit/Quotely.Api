using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quotely.Api.Data;
using Quotely.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Services --------------------
builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("db")));

builder.Services.AddRazorPages();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Quotely.Api", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer' [space] + token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

// JWT
builder.Services.AddSingleton<IJwtService, JwtService>();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization();

// CORS (wide-open for dev)
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// -------------------- Build app --------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// after var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate();
}


// -------------------- Middleware pipeline --------------------
app.UseStaticFiles();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// -------------------- Helpers --------------------
static bool TryGetUserId(ClaimsPrincipal me, out Guid userId)
{
    userId = default;
    var sid = me.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(sid)) return false;
    return Guid.TryParse(sid, out userId);
}

// -------------------- Minimal API endpoints --------------------

// Register
app.MapPost("/api/auth/register", async (AppDb db, Quotely.Api.Dtos.RegisterDto dto, IJwtService jwt) =>
{
    if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest("Email and password required.");

    if (await db.Users.AnyAsync(u => u.Email == dto.Email))
        return Results.BadRequest("Email already registered.");

    var user = new Quotely.Api.Models.User
    {
        Email = dto.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    var token = jwt.Generate(user);
    return Results.Ok(new { token });
})
.WithOpenApi();

// Login
app.MapPost("/api/auth/login", async (AppDb db, Quotely.Api.Dtos.LoginDto dto, IJwtService jwt) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = jwt.Generate(user);
    return Results.Ok(new { token });
})
.WithOpenApi();

// Create quote
app.MapPost("/api/quotes", async (ClaimsPrincipal me, AppDb db, Quotely.Api.Dtos.CreateQuoteDto dto) =>
{
    if (!TryGetUserId(me, out var userId))
        return Results.Unauthorized();

    var quote = new Quotely.Api.Models.Quote
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Text = dto.Text,
        SourceTitle = dto.SourceTitle,
        SourceAuthor = dto.SourceAuthor,
        SourceUrl = dto.SourceUrl,
        Note = dto.Note,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Quotes.Add(quote);

    var names = ((IEnumerable<string>?)dto.Tags ?? Enumerable.Empty<string>())
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    if (names.Count > 0)
    {
        // CHANGED: scope tags by user and set UserId on new tags
        var existing = await db.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync();

        var toAdd = names
            .Except(existing.Select(e => e.Name), StringComparer.InvariantCultureIgnoreCase)
            .Select(n => new Quotely.Api.Models.Tag
            {
                Id = Guid.NewGuid(),
                Name = n,
                UserId = userId
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Tags.AddRange(toAdd);
            await db.SaveChangesAsync();
        }

        var all = await db.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync();

        db.QuoteTags.AddRange(all.Select(t => new Quotely.Api.Models.QuoteTag
        {
            QuoteId = quote.Id,
            TagId = t.Id
        }));
    }

    await db.SaveChangesAsync();

    var result = new
    {
        id = quote.Id,
        text = quote.Text,
        sourceTitle = quote.SourceTitle,
        sourceAuthor = quote.SourceAuthor,
        sourceUrl = quote.SourceUrl,
        note = quote.Note,
        tags = await db.QuoteTags.Where(qt => qt.QuoteId == quote.Id).Select(qt => qt.Tag.Name).ToListAsync(),
        createdAt = quote.CreatedAt,
        updatedAt = quote.UpdatedAt
    };

    return Results.Created($"/api/quotes/{quote.Id}", result);
})
.RequireAuthorization()
.WithOpenApi();

// Get ALL quotes for current user
app.MapGet("/api/quotes", async (ClaimsPrincipal me, AppDb db) =>
{
    if (!TryGetUserId(me, out var userId))
        return Results.Unauthorized();

    var items = await db.Quotes
        .Where(x => x.UserId == userId)
        .Include(x => x.QuoteTags).ThenInclude(qt => qt.Tag)
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new
        {
            id = x.Id,
            text = x.Text,
            sourceTitle = x.SourceTitle,
            sourceAuthor = x.SourceAuthor,
            sourceUrl = x.SourceUrl,
            note = x.Note,
            tags = x.QuoteTags.Select(qt => qt.Tag.Name).ToList(),
            createdAt = x.CreatedAt,
            updatedAt = x.UpdatedAt
        })
        .ToListAsync();

    return Results.Ok(new { items });
})
.RequireAuthorization()
.WithOpenApi();

// Random quote
app.MapGet("/api/quotes/random", async (ClaimsPrincipal me, AppDb db, string? tag) =>
{
    if (!TryGetUserId(me, out var userId))
        return Results.Unauthorized();

    var query = db.Quotes.Where(x => x.UserId == userId);

    if (!string.IsNullOrWhiteSpace(tag))
        query = query.Where(x => x.QuoteTags.Any(qt => qt.Tag.Name == tag));

    var count = await query.CountAsync();
    if (count == 0) return Results.NotFound();

    var skip = Random.Shared.Next(count);

    var qx = await query
        .Include(x => x.QuoteTags).ThenInclude(qt => qt.Tag)
        .OrderBy(x => x.Id)
        .Skip(skip).Take(1)
        .Select(x => new
        {
            id = x.Id,
            text = x.Text,
            sourceTitle = x.SourceTitle,
            sourceAuthor = x.SourceAuthor,
            sourceUrl = x.SourceUrl,
            note = x.Note,
            tags = x.QuoteTags.Select(qt => qt.Tag.Name).ToList(),
            createdAt = x.CreatedAt,
            updatedAt = x.UpdatedAt
        })
        .FirstAsync();

    return Results.Ok(qx);
})
.RequireAuthorization()
.WithOpenApi();

// UPDATE quote
app.MapPut("/api/quotes/{id:guid}", async (
    ClaimsPrincipal me,
    AppDb db,
    Guid id,
    Quotely.Api.Dtos.UpdateQuoteDto dto) =>
{
    if (!TryGetUserId(me, out var userId))
        return Results.Unauthorized();

    var q = await db.Quotes
        .Include(x => x.QuoteTags)
        .ThenInclude(qt => qt.Tag)
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    if (q is null) return Results.NotFound();

    q.Text = dto.Text;
    q.SourceTitle = dto.SourceTitle;
    q.SourceAuthor = dto.SourceAuthor;
    q.SourceUrl = dto.SourceUrl;
    q.Note = dto.Note;
    q.UpdatedAt = DateTime.UtcNow;

    // rebuild tags
    db.QuoteTags.RemoveRange(q.QuoteTags);

    var names = ((IEnumerable<string>?)dto.Tags ?? Enumerable.Empty<string>())
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    if (names.Count > 0)
    {
        // CHANGED: scope by user and set UserId on new tags
        var existing = await db.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync();

        var toAdd = names
            .Except(existing.Select(e => e.Name), StringComparer.InvariantCultureIgnoreCase)
            .Select(n => new Quotely.Api.Models.Tag
            {
                Id = Guid.NewGuid(),
                Name = n,
                UserId = userId
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Tags.AddRange(toAdd);
            await db.SaveChangesAsync();
        }

        var all = await db.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync();

        db.QuoteTags.AddRange(all.Select(t => new Quotely.Api.Models.QuoteTag
        {
            QuoteId = q.Id,
            TagId = t.Id
        }));
    }

    await db.SaveChangesAsync();

    var result = new
    {
        id = q.Id,
        text = q.Text,
        sourceTitle = q.SourceTitle,
        sourceAuthor = q.SourceAuthor,
        sourceUrl = q.SourceUrl,
        note = q.Note,
        tags = await db.QuoteTags.Where(x => x.QuoteId == q.Id).Select(x => x.Tag.Name).ToListAsync(),
        createdAt = q.CreatedAt,
        updatedAt = q.UpdatedAt
    };

    return Results.Ok(result);
})
.RequireAuthorization()
.WithOpenApi();

// DELETE quote
app.MapDelete("/api/quotes/{id:guid}", async (ClaimsPrincipal me, AppDb db, Guid id) =>
{
    if (!TryGetUserId(me, out var userId))
        return Results.Unauthorized();

    var q = await db.Quotes
        .Include(x => x.QuoteTags)
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    if (q is null) return Results.NotFound();

    db.QuoteTags.RemoveRange(q.QuoteTags);
    db.Quotes.Remove(q);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization()
.WithOpenApi();

// -------------------- Run --------------------
app.Run();
