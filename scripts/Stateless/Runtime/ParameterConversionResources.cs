namespace Stateless
{
    public class ParameterConversionResources
    {
        public const string ArgOfTypeRequiredInPosition = "Parameter at position {1} must be of type '{0}', but was not provided.";
        public const string WrongArgType = "Invalid type for parameter at position {0}. Expected '{2}', but received '{1}'.";
        public const string TooManyParameters = "Invalid number of parameters. Method expects {0} parameters, but {1} were provided.";
    }
}