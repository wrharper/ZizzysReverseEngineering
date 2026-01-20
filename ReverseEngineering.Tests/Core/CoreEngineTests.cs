using Xunit;
using ReverseEngineering.Core;
using ReverseEngineering.Core.Keystone;
using System;
using System.IO;

namespace ReverseEngineering.Tests.Core
{
    /// <summary>
    /// Basic smoke tests for core RE functionality
    /// </summary>
    public class HexBufferTests
    {
        [Fact]
        public void Constructor_InitializesBytes()
        {
            // Arrange
            byte[] testBytes = { 0x90, 0xC3, 0x55 };

            // Act
            var buffer = new HexBuffer(testBytes, "test.bin");

            // Assert
            Assert.NotNull(buffer.Bytes);
            Assert.Equal(3, buffer.Bytes.Length);
        }

        [Fact]
        public void WriteByte_MarksModified()
        {
            // Arrange
            byte[] testBytes = { 0x90, 0xC3, 0x55 };
            var buffer = new HexBuffer(testBytes, "test.bin");

            // Act
            buffer.WriteByte(0, 0xCC);

            // Assert
            Assert.True(buffer.Modified[0]);
            Assert.Equal(0xCC, buffer.Bytes[0]);
        }

        [Fact]
        public void Indexer_ReturnsCorrectByte()
        {
            // Arrange
            byte[] testBytes = { 0x90, 0xC3, 0x55 };
            var buffer = new HexBuffer(testBytes);

            // Act & Assert
            Assert.Equal(0x90, buffer[0]);
            Assert.Equal(0xC3, buffer[1]);
            Assert.Equal(0x55, buffer[2]);
        }

        [Fact]
        public void WriteBytes_UpdatesMultipleBytes()
        {
            // Arrange
            byte[] testBytes = { 0x00, 0x00, 0x00, 0x00 };
            var buffer = new HexBuffer(testBytes);

            // Act
            buffer.WriteBytes(0, new[] { (byte)0xCC, (byte)0xCC });

            // Assert
            Assert.Equal(0xCC, buffer[0]);
            Assert.Equal(0xCC, buffer[1]);
            Assert.True(buffer.Modified[0]);
            Assert.True(buffer.Modified[1]);
        }
    }

    /// <summary>
    /// Tests for KeystoneAssembler static methods
    /// </summary>
    public class KeystoneAssemblerTests
    {
        [Fact]
        public void Assemble_NOPx64_ProducesCorrectBytes()
        {
            // Act
            var bytes = KeystoneAssembler.Assemble("NOP", 0x400000, true);

            // Assert
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
            Assert.Equal(0x90, bytes[0]); // NOP = 0x90
        }

        [Fact]
        public void Assemble_RETx64_ProducesCorrectBytes()
        {
            // Act
            var bytes = KeystoneAssembler.Assemble("RET", 0x400000, true);

            // Assert
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
            Assert.Equal(0xC3, bytes[0]); // RET = 0xC3
        }

        [Fact]
        public void Assemble_Invalidx64_ReturnsEmptyOrThrows()
        {
            // Act & Assert - either returns empty array or throws
            try
            {
                var bytes = KeystoneAssembler.Assemble("INVALID_INSTRUCTION_XYZ", 0x400000, true);
                // If it doesn't throw, it should return empty
                Assert.True(bytes == null || bytes.Length == 0, "Invalid instruction should return null or empty");
            }
            catch (Exception)
            {
                // Exception is also acceptable
            }
        }

        [Fact]
        public void Assemble_x86_32bit()
        {
            // Act
            var bytes = KeystoneAssembler.Assemble("NOP", 0x400000, false);

            // Assert
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
            Assert.Equal(0x90, bytes[0]);
        }
    }

    /// <summary>
    /// Tests for Instruction class
    /// </summary>
    public class InstructionTests
    {
        [Fact]
        public void Instruction_CanStoreBasicProperties()
        {
            // Arrange & Act
            var instruction = new Instruction
            {
                Address = 0x400000,
                Mnemonic = "MOV",
                Operands = "RAX, 0x1234"
            };

            // Assert
            Assert.Equal(0x400000UL, instruction.Address);
            Assert.Equal("MOV", instruction.Mnemonic);
            Assert.Equal("RAX, 0x1234", instruction.Operands);
        }
    }

    /// <summary>
    /// Tests for PatchEngine - change tracking
    /// </summary>
    public class PatchEngineTests
    {
        [Fact]
        public void Constructor_RequiresHexBuffer()
        {
            // Arrange
            var buffer = new HexBuffer(new byte[] { 0x90, 0xC3 });

            // Act
            var patchEngine = new PatchEngine(buffer);

            // Assert
            Assert.NotNull(patchEngine);
            Assert.NotNull(patchEngine.Patches);
        }

        [Fact]
        public void Patches_StartsEmpty()
        {
            // Arrange
            var buffer = new HexBuffer(new byte[] { 0x90, 0xC3 });
            var patchEngine = new PatchEngine(buffer);

            // Act & Assert
            Assert.Empty(patchEngine.Patches);
        }
    }
}

