namespace Stateless
{
    public class StateMachineResources
    {
        public const string CannotReconfigureParameters = "Trigger '{0}' parameters cannot be changed once set.";
        public const string NoTransitionsUnmetGuardConditions = "Cannot trigger '{0}' from state '{1}' because the following guard conditions were not met: {2}";
        public const string NoTransitionsPermitted = "No transitions are permitted from state '{1}' for trigger '{0}'.";
    }
}