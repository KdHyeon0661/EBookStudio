using System.Text.Json;
using System.IO;
using EBookStudio.Helpers;
using EBookStudio.Services;
using Xunit;

namespace EBookStudio.Tests;

public sealed class CommandAndSettingsTests
{
    [Fact]
    public void UiCommandRoutesAsyncFailuresToItsErrorHandler()
    {
        Exception? observed = null;
        var command = new AsyncRelayCommand(
            _ => Task.FromException(new InvalidOperationException("expected failure")),
            onError: error => observed = error);

        command.Execute(null);

        var error = Assert.IsType<InvalidOperationException>(observed);
        Assert.Equal("expected failure", error.Message);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void FontSizeAndLineHeightAreIndependentAndPersisted()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            SettingsService settings = SettingsService.Instance;
            settings.FontSize = 18;
            settings.LineHeight = 52;

            Assert.Equal(18, settings.FontSize);
            Assert.Equal(52, settings.LineHeight);

            string path = Path.Combine(FileHelper.BasePath, "settings.json");
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(18, saved.RootElement.GetProperty("FontSize").GetDouble());
            Assert.Equal(52, saved.RootElement.GetProperty("LineHeight").GetDouble());
        });
    }
}
