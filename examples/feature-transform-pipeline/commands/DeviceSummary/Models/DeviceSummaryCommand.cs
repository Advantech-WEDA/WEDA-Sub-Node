using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace FeatureTransformPipeline.commands.DeviceSummary.Models;

[DeviceCmd("device.summary")]
[Display(Name = "Device Summary")]
[Description("Collect user-facing device info: identity, IP, sensor count, and sensor inventory.")]
public class DeviceSummaryCommand : CommandData<DeviceSummaryParameters>;

public class DeviceSummaryParameters;
