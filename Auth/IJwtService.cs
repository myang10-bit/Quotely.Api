using Quotely.Api.Models;

namespace Quotely.Api.Auth
{
    public interface IJwtService
    {
        string Generate(User user);
    }
}
