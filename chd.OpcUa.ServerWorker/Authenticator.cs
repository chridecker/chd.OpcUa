using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Text;
namespace chd.OpcUa.ServerWorker
{
    public static class Authenticator
    {
        public sealed record SystemUser(string UserName, string Password, Role Role);
        public static IReadOnlyList<SystemUser> All { get; } =
        [
            new SystemUser("observer1", "observer1", Role.Observer),
            new SystemUser("operator1", "operator1", Role.Operator),
            new SystemUser("engineer1", "engineer1", Role.Engineer),
            new SystemUser("supervisor1", "supervisor1", Role.Supervisor),
            new SystemUser("secadmin", "secadmin", Role.SecurityAdmin),
            new SystemUser("guest", "guest", null),
        ];


        public static void ConfigureRoles(RoleConfigurationOptions roles)
        {
            foreach (var user in All)
            {
                if (user.Role is null)
                {
                    continue;
                }

                roles.Roles.Add(new RoleDefinitionOptions
                {
                    Name = user.Role.Name,
                    Identities =
                    {
                        new RoleIdentityMappingOptions
                            { CriteriaType = IdentityCriteriaType.UserName, Criteria = user.UserName, },
                    },
                });
            }
        }
        public static ValueTask<IUserIdentity> AuthenticateAsync(UserNameIdentityTokenHandler handler, CancellationToken ct)
        {
            string password = handler.DecryptedPassword != null
                ? Encoding.UTF8.GetString(handler.DecryptedPassword)
                : null;

            foreach (var user in All)
            {
                if (string.Equals(user.UserName, handler.UserName, StringComparison.Ordinal) &&
                    string.Equals(user.Password, password, StringComparison.Ordinal))
                {
                    return new ValueTask<IUserIdentity>(new UserIdentity(handler));
                }
            }

            throw ServiceResultException.Create(StatusCodes.BadUserAccessDenied, "'{0}' is not one of the sample accounts, or the password is wrong.", handler.UserName);
        }
    }
}
