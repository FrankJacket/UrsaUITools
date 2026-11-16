using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;

namespace Ursa.UITools;

/// <summary>
/// A non-modal, draggable and resizable dialog that floats above the page.
/// It reuses the MudBlazor dialog look (title, content and actions areas) but has no overlay,
/// so the page behind stays interactive. The header and footer stay visible while the content scrolls,
/// and all three areas adapt to the dialog's own width through container queries.
/// </summary>
public partial class UrsaFloatingDialog : MudComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Ursa.UITools/Dialogs/UrsaFloatingDialog.razor.js";

    private static readonly string[] ResizeDirections = ["n", "s", "e", "w", "ne", "nw", "se", "sw"];

    private readonly string _id = $"ursa-floating-dialog-{Guid.NewGuid():N}";
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<UrsaFloatingDialog>? _selfReference;
    private bool _jsInitialized;

    private string _titleId => $"{_id}-title";

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Whether the dialog is shown.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Raised when <see cref="Visible"/> changes (for example when the close button is used).</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Plain-text title shown in the header. Ignored when <see cref="TitleContent"/> is set.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Custom header content.</summary>
    [Parameter]
    public RenderFragment? TitleContent { get; set; }

    /// <summary>Main content. It scrolls when it does not fit; the header and footer stay in place.</summary>
    [Parameter]
    public RenderFragment? DialogContent { get; set; }

    /// <summary>Footer content, typically buttons. The footer is omitted when not provided.</summary>
    [Parameter]
    public RenderFragment? DialogActions { get; set; }

    /// <summary>Shows the close button in the header.</summary>
    [Parameter]
    public bool ShowCloseButton { get; set; } = true;

    private static readonly UrsaLoc L = new();

    /// <summary>Accessible label of the close button. Defaults to the translation of "Close".</summary>
    [Parameter]
    public string? CloseButtonLabel { get; set; }

    /// <summary>Closes the dialog when Escape is pressed while it has focus.</summary>
    [Parameter]
    public bool CloseOnEscapeKey { get; set; }

    /// <summary>Allows moving the dialog by dragging its header.</summary>
    [Parameter]
    public bool Draggable { get; set; } = true;

    /// <summary>Allows resizing the dialog from its edges and corners.</summary>
    [Parameter]
    public bool Resizable { get; set; } = true;

    /// <summary>Initial width in pixels. Reduced automatically to fit the viewport.</summary>
    [Parameter]
    public int InitialWidth { get; set; } = 480;

    /// <summary>Initial height in pixels. When null, the height follows the content until the user resizes.</summary>
    [Parameter]
    public int? InitialHeight { get; set; }

    /// <summary>Initial distance from the left of the viewport. When null, the dialog is centered.</summary>
    [Parameter]
    public int? InitialX { get; set; }

    /// <summary>Initial distance from the top of the viewport. When null, the dialog is centered.</summary>
    [Parameter]
    public int? InitialY { get; set; }

    /// <summary>Minimum width in pixels when resizing.</summary>
    [Parameter]
    public int MinWidth { get; set; } = 240;

    /// <summary>Minimum height in pixels when resizing.</summary>
    [Parameter]
    public int MinHeight { get; set; } = 160;

    /// <summary>Raised after the user finishes moving or resizing the dialog.</summary>
    [Parameter]
    public EventCallback<UrsaDialogBounds> OnBoundsChanged { get; set; }

    private string Classname =>
        new CssBuilder("ursa-floating-dialog")
            .AddClass(Class)
            .Build();

    private string? Stylename =>
        new StyleBuilder()
            .AddStyle(Style)
            .Build();

    /// <summary>Closes the dialog.</summary>
    public async Task CloseAsync()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(false);
    }

    /// <summary>Called from JavaScript when a move or resize gesture ends.</summary>
    [JSInvokable]
    public Task NotifyBoundsChanged(double x, double y, double width, double height) =>
        OnBoundsChanged.InvokeAsync(new UrsaDialogBounds(x, y, width, height));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible && !_jsInitialized)
        {
            _jsInitialized = true;
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _selfReference ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("initialize", _root, _id, _selfReference, new
            {
                width = InitialWidth,
                height = InitialHeight,
                x = InitialX,
                y = InitialY,
                minWidth = MinWidth,
                minHeight = MinHeight,
            });
        }
        else if (!Visible && _jsInitialized)
        {
            _jsInitialized = false;
            await DisposeScriptAsync();
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (CloseOnEscapeKey && args.Key == "Escape")
        {
            await CloseAsync();
        }
    }

    private async Task DisposeScriptAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("dispose", _id);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            // The circuit is gone; nothing to clean up.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsInitialized)
        {
            _jsInitialized = false;
            await DisposeScriptAsync();
        }

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
            {
            }
        }

        _selfReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}