using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hospitality.API.Middleware;

public class ValidationExceptionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly MethodInfo ValidateMethod = typeof(ValidationExceptionFilter)
        .GetMethod(nameof(ValidateArgumentAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public ValidationExceptionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = _serviceProvider.GetService(validatorType);
            if (validator is null)
            {
                continue;
            }

            var validateTask = (Task)ValidateMethod.MakeGenericMethod(argumentType).Invoke(null, new[] { validator, argument })!;
            await validateTask;
            var failures = (IList<FluentValidation.Results.ValidationFailure>)validateTask.GetType()
                .GetProperty("Result")!.GetValue(validateTask)!;

            if (failures.Count > 0)
            {
                var errors = failures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

                context.Result = new BadRequestObjectResult(new
                {
                    statusCode = 400,
                    message = "Solicitud no válida.",
                    details = errors
                });
                return;
            }
        }

        await next();
    }

    private static async Task<IList<FluentValidation.Results.ValidationFailure>> ValidateArgumentAsync<T>(IValidator validator, object argument)
        where T : class
    {
        var typedValidator = (IValidator<T>)validator;
        var result = await typedValidator.ValidateAsync((T)argument);
        return result.Errors;
    }
}