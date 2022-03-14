using HsBot.Logic;

namespace Discord.Commands
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequireSpecificAllianceRoleAttribute : PreconditionAttribute
    {
        public override string ErrorMessage { get; set; }
        public string NotAGuildErrorMessage { get; set; }

        private readonly AllianceRole[] _allianceRoles;

        public RequireSpecificAllianceRoleAttribute(AllianceRole[] allianceRoles)
        {
            _allianceRoles = allianceRoles;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User is not IGuildUser guildUser)
                return Task.FromResult(PreconditionResult.FromError(NotAGuildErrorMessage ?? "Command must be used in a guild channel."));

            var alliance = AllianceLogic.GetAlliance(guildUser.GuildId);
            if (alliance == null)
                return Task.FromResult(PreconditionResult.FromSuccess());

            var requiredRoles = _allianceRoles
                .Select(x => guildUser.Guild.GetRole(alliance.GetAllianceRoleId(x)))
                .Where(x => x != null)
                .ToList();

            if (requiredRoles.Any(x => guildUser.RoleIds.Any(y => y == x.Id)))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError(ErrorMessage ?? "You need one of these roles to use this command: " + string.Join(", ", requiredRoles.Select(x => "`" + x.Name + "`"))));
        }
    }
}