using Xunit;
using FastP.Services;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace FastP.Tests
{
    public class ConfigManagerTests
    {
        [Fact]
        public void GetTargetFolder_ShouldReturnCorrectFolder_ForKnownExtension()
        {
            // Arrange
            var configManager = new ConfigManager();
            
            // Act
            var folderJpg = configManager.GetTargetFolder(".jpg");
            var folderMp3 = configManager.GetTargetFolder(".mp3");

            // Assert
            Assert.Equal("Images", folderJpg);
            Assert.Equal("Audio", folderMp3);
        }

        [Fact]
        public void GetTargetFolder_ShouldReturnNull_ForUnknownExtension()
        {
            // Arrange
            var configManager = new ConfigManager();

            // Act
            var result = configManager.GetTargetFolder(".unknown_extension_123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetTargetFolder_ShouldHandleExtensionWithoutDot()
        {
            // Arrange
            var configManager = new ConfigManager();

            // Act
            var result = configManager.GetTargetFolder("jpg");

            // Assert
            Assert.Equal("Images", result);
        }
    }
}

