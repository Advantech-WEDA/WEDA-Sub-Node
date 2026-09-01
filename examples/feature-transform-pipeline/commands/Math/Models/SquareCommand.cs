using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace FeatureTransformPipeline.commands.Math.Models;

[DeviceCmd("math.square")]
[Display(Name = "Math square")]
[Description("Calculate the square of input number.")]
public class SquareCommand : CommandData<SquareParameters>
{
}

/// <summary>
/// gpio.list takes no parameters: it always reports every IGpioPinListable device.
/// </summary>
public class SquareParameters
{
    [Required]
    [Range(-32768, 32768)]
    [Description("The number you want to set input for the calculation.")]
    public int Value { get; set; }
}
