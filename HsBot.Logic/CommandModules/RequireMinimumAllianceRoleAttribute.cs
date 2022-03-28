namespace HsBot.Logic;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireMinimumAllianceRoleAttribute : PreconditionAttribute
{
    public AllianceRole AllianceRole { get; }

    public RequireMinimumAllianceRoleAttribute(AllianceRole allianceRole)
    {
        AllianceRole = allianceRole;
    }

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
            return Task.FromResult(PreconditionResult.FromError("Command must be used in a guild channel."));

        var alliance = AllianceLogic.GetAlliance(guildUser.GuildId);
        if (alliance == null)
            return Task.FromResult(PreconditionResult.FromSuccess());

        var roleId = AllianceRole switch
        {
            AllianceRole.Greeter => alliance.GreeterRoleId,
            _ => 0UL,
        };

        var role = guildUser.Guild.GetRole(roleId);
        if (role == null
            || guildUser.RoleIds.Any(x => guildUser.Guild.GetRole(x).Position >= role.Position))
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(PreconditionResult.FromError("You need `" + role.Name + "` or higher role to use this command."));
    }

    public bool Test(IUser user)
    {
        if (user is not IGuildUser guildUser)
            return false;

        var alliance = AllianceLogic.GetAlliance(guildUser.GuildId);
        if (alliance == null)
            return false;

        var role = guildUser.Guild.GetRole(alliance.GetAllianceRoleId(AllianceRole));
        return role == null
            || guildUser.RoleIds.Any(x => guildUser.Guild.GetRole(x).Position >= role.Position);
    }
}
