using Shouldly;
using Weda.SubNode.Core.Protocols.Modbus;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Modbus;

public class ModbusCrc16Tests
{
    [Fact]
    public void Calculate_ReadHoldingRegisters_ReturnsCorrectCrc()
    {
        // SlaveId=1, FC=03, Address=0000, Count=0001
        // CRC = 0x0A84, in frame as little-endian: [84, 0A]
        byte[] data = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01];

        var crc = ModbusCrc16.Calculate(data);

        crc.ShouldBe((ushort)0x0A84);
    }

    [Fact]
    public void Calculate_ReadInputRegisters_ReturnsCorrectCrc()
    {
        // SlaveId=1, FC=04, Address=0000, Count=0001
        byte[] data = [0x01, 0x04, 0x00, 0x00, 0x00, 0x01];

        var crc = ModbusCrc16.Calculate(data);

        // Actual CRC from calculation
        crc.ShouldBe((ushort)0xCA31);
    }

    [Fact]
    public void Verify_ValidFrame_ReturnsTrue()
    {
        // Complete RTU frame with valid CRC (0x0A84 as little-endian: 84 0A)
        byte[] frame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A];

        var result = ModbusCrc16.Verify(frame);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Verify_InvalidFrame_ReturnsFalse()
    {
        // Frame with wrong CRC
        byte[] frame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF];

        var result = ModbusCrc16.Verify(frame);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Verify_FrameTooShort_ReturnsFalse()
    {
        byte[] frame = [0x01, 0x03];

        var result = ModbusCrc16.Verify(frame);

        result.ShouldBeFalse();
    }

    [Fact]
    public void AppendCrc_AddsCorrectCrc()
    {
        byte[] data = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01];

        var frame = ModbusCrc16.AppendCrc(data);

        frame.Length.ShouldBe(8);
        // CRC = 0x0A84, little-endian: low byte (0x84) first, high byte (0x0A) second
        frame[6].ShouldBe((byte)0x84);
        frame[7].ShouldBe((byte)0x0A);
    }

    [Fact]
    public void AppendCrc_ResultPassesVerify()
    {
        byte[] data = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01];

        var frame = ModbusCrc16.AppendCrc(data);
        var isValid = ModbusCrc16.Verify(frame);

        isValid.ShouldBeTrue();
    }
}
