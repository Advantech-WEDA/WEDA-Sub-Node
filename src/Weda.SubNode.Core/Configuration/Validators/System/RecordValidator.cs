using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators.System;

/// <summary>
/// Validates Recording configuration.
/// Delegates to SubNodeRecordConfigDto.TryValidate() for value range validation.
/// </summary>
public class RecordValidator : ISystemConfigPropertyValidator
{
    public string PropertyName => "Record";

    public ConfigurationValidationResult Validate(SystemConfigValidationContext context)
    {
        var record = context.DesiredConfig.Record;
        if (record == null)
            return ConfigurationValidationResult.Success;

        if (!record.TryValidate(out var errorMessage))
        {
            return ConfigurationValidationResult.Failure(errorMessage!);
        }

        return ConfigurationValidationResult.Success;
    }
}
