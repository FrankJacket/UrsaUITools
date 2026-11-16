using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>The format of <see cref="UrsaEditor.Value"/>.</summary>
public enum UrsaEditorFormat
{
    /// <summary>HTML, as produced by Quill.</summary>
    Html,

    /// <summary>Markdown, converted from and to the editor's HTML on the server.</summary>
    Markdown,
}

/// <summary>The tools shown in the toolbar of <see cref="UrsaEditor"/>.</summary>
[Flags]
public enum UrsaEditorTools
{
    /// <summary>No toolbar.</summary>
    None = 0,

    /// <summary>Bold.</summary>
    Bold = 1,

    /// <summary>Italic.</summary>
    Italic = 2,

    /// <summary>Underline. Markdown has no underline, so it is lost when the format is Markdown.</summary>
    Underline = 4,

    /// <summary>Strikethrough.</summary>
    Strike = 8,

    /// <summary>Headings 1 to 3.</summary>
    Headings = 16,

    /// <summary>Bulleted and numbered lists.</summary>
    Lists = 32,

    /// <summary>Block quote.</summary>
    Quote = 64,

    /// <summary>Code block.</summary>
    Code = 128,

    /// <summary>Clear formatting.</summary>
    Clean = 256,

    /// <summary>Undo and redo.</summary>
    History = 512,

    /// <summary>Insert and edit a link.</summary>
    Link = 1024,

    /// <summary>Insert an image, from an address or an uploaded file.</summary>
    Image = 2048,

    /// <summary>The tools Markdown can express.</summary>
    Markdown = Bold | Italic | Strike | Headings | Lists | Quote | Code | Clean | History | Link | Image,

    /// <summary>Every tool.</summary>
    All = Markdown | Underline,
}

/// <summary>
/// A rich text editor based on Quill, as HTML or Markdown. The toolbar and the frame are made of MudBlazor
/// components and CSS variables, so the editor follows the theme of the application (light and dark).
/// Editing runs in the browser; the server receives the value after a pause in the typing or on blur, never on each
/// key stroke.
/// <para>
/// The HTML that comes out of the editor only contains what Quill knows (it drops everything else when loading), but
/// treat it as untrusted user input and sanitize it before showing it elsewhere.
/// </para>
/// </summary>
public partial class UrsaEditor : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Ursa.UITools/Editors/UrsaEditor.razor.js";

    private ElementReference _root;
    private ElementReference _surface;
    private IJSObjectReference? _module;
    private DotNetObjectReference<UrsaEditor>? _self;
    private string? _committed;
    private string? _error;
    private bool _created;
    private bool _pendingValue = true;
    private string? _lastOptions;
    private readonly System.Text.StringBuilder _chunks = new();
    private string? _chunkId;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The content, in the format given by <see cref="Format"/>.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised when the content is committed (after a pause in the typing, or on blur).</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>The format of <see cref="Value"/>.</summary>
    [Parameter]
    public UrsaEditorFormat Format { get; set; } = UrsaEditorFormat.Html;

    /// <summary>The tools shown in the toolbar. Defaults to every tool, or to the ones Markdown can express.</summary>
    [Parameter]
    public UrsaEditorTools? Tools { get; set; }

    /// <summary>Label shown above the editor.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Placeholder shown while the editor is empty.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>Helper text shown under the editor.</summary>
    [Parameter]
    public string? HelperText { get; set; }

    /// <summary>An error message; the frame turns to the error color while it is set.</summary>
    [Parameter]
    public string? ErrorText { get; set; }

    /// <summary>Makes the editor read-only.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>Disables the editor.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Minimum height of the writing area, as a CSS length.</summary>
    [Parameter]
    public string MinHeight { get; set; } = "160px";

    /// <summary>Maximum height of the writing area, as a CSS length; it scrolls beyond it.</summary>
    [Parameter]
    public string? MaxHeight { get; set; }

    /// <summary>Milliseconds of silence after typing before the value is committed.</summary>
    [Parameter]
    public int CommitDelay { get; set; } = 700;

    /// <summary>CSS class of the root element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Inline style of the root element.</summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>
    /// Stretches the editor to the bottom of its container instead of growing with its content: the writing area takes
    /// all the height left and scrolls inside. The container must be a flex column (or have a fixed height), for example the
    /// content of an <see cref="UrsaFloatingDialog"/> wrapped in <c>d-flex flex-column</c>. <see cref="MinHeight"/> still applies;
    /// <see cref="MaxHeight"/> is ignored.
    /// </summary>
    [Parameter]
    public bool FillHeight { get; set; }

    private UrsaEditorTools EffectiveTools =>
        Tools ?? (Format == UrsaEditorFormat.Markdown ? UrsaEditorTools.Markdown : UrsaEditorTools.All);

    private string RootClass => string.Join(' ', new[]
    {
        "ursa-editor",
        Disabled ? "ursa-editor-disabled" : null,
        FillHeight ? "ursa-editor-fill" : null,
        ErrorText is not null || _error is not null ? "ursa-editor-error" : null,
        Class,
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string SurfaceStyle =>
        $"min-height: {MinHeight};{(MaxHeight is null || FillHeight ? "" : $" max-height: {MaxHeight}; overflow-y: auto;")}";

    private string? _panel;
    private string? _panelUrl;
    private string? _panelAlt;
    private int? _panelWidth;
    private string? _panelError;
    private bool _panelHadLink;

    /// <summary>Maximum size in bytes of an uploaded image that is embedded in the content.</summary>
    [Parameter]
    public long MaxImageSize { get; set; } = 1_000_000;

    /// <summary>
    /// Stores an uploaded image and returns its address, for example a file or a blob in your storage. When it is not
    /// set, the image is embedded in the content as a data URL, which makes the HTML or Markdown large.
    /// </summary>
    [Parameter]
    public Func<IBrowserFile, Task<string>>? ImageUploader { get; set; }

    private static readonly UrsaLoc L = new();

    private static readonly DialogOptions DialogOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

    private string PanelTitle => _panel switch
    {
        "link" => L["Link"],
        "image-edit" => L["Image properties"],
        _ => L["Insert image"],
    };

    private string PanelAction => _panel switch
    {
        "link" => string.IsNullOrWhiteSpace(_panelUrl) && _panelHadLink ? L["Remove link"] : L["Apply"],
        "image-edit" => L["Apply"],
        _ => L["Insert"],
    };

    /// <summary>Called from JavaScript when the link or image button is pressed, or an image is edited.</summary>
    [JSInvokable]
    public Task OnToolRequested(string tool, string? current, string? alt, int? width)
    {
        _panel = tool;
        _panelUrl = tool == "link" ? current : null;
        _panelHadLink = tool == "link" && !string.IsNullOrEmpty(current);
        _panelAlt = alt;
        _panelWidth = width;
        _panelError = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ClosePanel() => _panel = null;

    private void OnDialogVisibleChanged(bool visible)
    {
        if (!visible)
        {
            _panel = null;
        }
    }

    private async Task ApplyPanelAsync()
    {
        var url = _panelUrl?.Trim();
        if (_module is null || _panel is null)
        {
            return;
        }

        if (_panel == "image" && string.IsNullOrEmpty(url))
        {
            _panelError = L["Enter an address or upload an image"];
            return;
        }

        try
        {
            switch (_panel)
            {
                case "link":
                    await _module.InvokeVoidAsync("applyLink", _root, url);
                    break;
                case "image-edit":
                    await _module.InvokeVoidAsync("updateImage", _root, _panelAlt, _panelWidth);
                    break;
                default:
                    await _module.InvokeVoidAsync("insertImage", _root, url, _panelAlt, _panelWidth);
                    break;
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            return; // the circuit is gone
        }

        _panel = null;
    }

    private async Task OnImagePickedAsync(IBrowserFile? file)
    {
        if (file is null)
        {
            return;
        }

        _panelError = null;
        try
        {
            if (ImageUploader is not null)
            {
                _panelUrl = await ImageUploader(file);
                return;
            }

            await using var stream = file.OpenReadStream(MaxImageSize);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            _panelUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (IOException)
        {
            _panelError = L["The image is larger than {0} KB", MaxImageSize / 1000];
        }
    }

    /// <summary>Called from JavaScript with a piece of a content that is too large for a single message.</summary>
    [JSInvokable]
    public async Task OnEditorChunk(string id, int index, int total, string part)
    {
        if (index == 0)
        {
            _chunkId = id;
            _chunks.Clear();
        }
        else if (id != _chunkId)
        {
            return; // pieces of an abandoned content
        }

        _chunks.Append(part);
        if (index == total - 1)
        {
            var html = _chunks.ToString();
            _chunks.Clear();
            await OnEditorCommit(html);
        }
    }

    /// <summary>Called from JavaScript when the content is committed.</summary>
    [JSInvokable]
    public async Task OnEditorCommit(string html)
    {
        var value = Format == UrsaEditorFormat.Markdown ? UrsaEditorConverter.HtmlToMarkdown(html) : html;
        if (value == _committed)
        {
            return;
        }

        _committed = value;
        await ValueChanged.InvokeAsync(value);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _error = ErrorText;
        if (Value != _committed)
        {
            _committed = Value;
            _pendingValue = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var options = $"{Placeholder}|{ReadOnly || Disabled}|{CommitDelay}";
        try
        {
            if (!_created)
            {
                _created = true;
                _lastOptions = options;
                _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
                _self ??= DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync("create", _root, _surface, _self,
                    new { placeholder = Placeholder, readOnly = ReadOnly || Disabled, delay = CommitDelay });
            }
            else if (_lastOptions != options && _module is not null)
            {
                _lastOptions = options;
                await _module.InvokeVoidAsync("configure", _root,
                    new { placeholder = Placeholder, readOnly = ReadOnly || Disabled, delay = CommitDelay });
            }

            if (_pendingValue && _module is not null)
            {
                _pendingValue = false;
                var html = Format == UrsaEditorFormat.Markdown ? UrsaEditorConverter.MarkdownToHtml(Value) : Value ?? "";
                await _module.InvokeVoidAsync("setHtml", _root, html);
            }
        }
        catch (JSDisconnectedException)
        {
            // the circuit is already gone
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("destroy", _root);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // the circuit is already gone
        }

        _self?.Dispose();
        GC.SuppressFinalize(this);
    }
}
