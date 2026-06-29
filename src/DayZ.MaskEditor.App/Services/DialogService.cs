using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DayZ.MaskEditor.App.Services;

public interface IDialogService
{
    Task<string?> OpenFileAsync(string title, params (string Name, string[] Patterns)[] filters);
    Task<string?> SaveFileAsync(string title, string suggestedName, string extension);
}

/// <summary>StorageProvider-backed file pickers bound to a top-level window.</summary>
public sealed class DialogService : IDialogService
{
    private readonly TopLevel _top;
    public DialogService(TopLevel top) => _top = top;

    public async Task<string?> OpenFileAsync(
        string title, params (string Name, string[] Patterns)[] filters)
    {
        var types = filters.Select(f => new FilePickerFileType(f.Name)
        {
            Patterns = f.Patterns,
        }).ToList();

        var files = await _top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = types,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, string extension)
    {
        var file = await _top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } },
            },
        });
        return file?.TryGetLocalPath();
    }
}
