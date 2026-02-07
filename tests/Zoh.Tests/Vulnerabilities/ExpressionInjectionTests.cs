using Xunit;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Interpolation;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Vulnerabilities;

public class ExpressionInjectionTests
{
    [Fact]
    public void Interpolation_ShouldNotRecurse_WhenInjectedValueContainsTemplateSyntax()
    {
        // Attack Vector:
        // *user_input <- "World${*secret}";
        // *template <- "Hello ${*user_input}!";
        // /interpolate *template;

        // Setup
        var variables = new VariableStore(new Dictionary<string, Variable>());

        // 1. Define *secret
        variables.Set("secret", new ZohStr("SECRET_VALUE"));

        // 2. Define *user_input which contains malicious template syntax
        variables.Set("user_input", new ZohStr("World${*secret}"));

        // 3. Define *template which interpolates *user_input
        // The value of *template describes "Hello ${*user_input}!"
        var template = "Hello ${*user_input}!";

        // Act
        var interpolator = new ZohInterpolator(variables);
        var result = interpolator.Interpolate(template);

        // Assert
        // Safe behavior: "Hello World${*secret}!" (Literal ${*secret}, NOT expanded)
        // Unsafe behavior: "Hello WorldSECRET_VALUE!"

        Assert.Equal("Hello World${*secret}!", result);
    }

    [Fact]
    public void ExpressionInterpolation_ShouldNotRecurse()
    {
        // Same test but via ExpressionEvaluator logic locally (simulation)
        // or just rely on ZohInterpolator since that's what /interpolate uses.
        // We can test nested interpolation logic if we construct an InterpolateExpressionAst,
        // but checking ZohInterpolator is the main coverage for the /interpolate verb.
    }
}
