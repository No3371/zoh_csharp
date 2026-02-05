using Zoh.Runtime.Types;

namespace Zoh.Runtime.Variables;

public record Variable(ZohValue Value, ZohValueType? TypeConstraint = null)
{
    public Variable WithValue(ZohValue value)
    {
        if (TypeConstraint.HasValue && value.Type != TypeConstraint.Value)
        {
            // Allow casting if applicable? Spec says /type verb enforces constraint.
            // If assigning incompatible type, it should error.
            // Strict check here?
            if (value.Type != TypeConstraint.Value)
                throw new InvalidOperationException($"Variable type mismatch: Expected {TypeConstraint}, got {value.Type}");
        }
        return this with { Value = value };
    }
}
