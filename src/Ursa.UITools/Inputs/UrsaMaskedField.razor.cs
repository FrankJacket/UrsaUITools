using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>When <see cref="UrsaMaskedField"/> hands its value to the application.</summary>
public enum UrsaMaskCommit
{
    /// <summary>When the field loses focus or Enter is pressed.</summary>
    Blur,

    /// <summary>As soon as the mask is complete, and also on blur or Enter.</summary>
    Complete,
}

/// <summary>
/// A text field with an input mask that runs entirely in the browser. Unlike <c>MudTextField</c> with a
/// <c>Mask</c>, which edits the text on the server at every key stroke (a SignalR round trip each time in
/// Interactive Server mode), the server only receives the value when it is committed, so typing never lags,
/// loses characters or moves the caret.
/// <para>
/// Mask syntax: <c>0</c> is a digit, <c>a</c> a letter, <c>*</c> any character; anything else is a literal that
/// is inserted for you. Typing in the middle of a complete value overwrites, and Backspace/Delete in the middle
/// only move over the character, so a value is never shifted or erased by accident.
/// </para>
/// </summary>
public partial class UrsaMaskedField : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Ursa.UITools/Inputs/UrsaMaskedField.razor.js";

    private ElementReference _wrapper;
    private IJSObjectReference? _module;
    private DotNetObjectReference<UrsaMaskedField>? _self;
    private string? _text;
    private string? _committed;
    private string? _error;
    private string? _attachedKey;
    private bool _pendingSync;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The mask, for example <c>00/00/0000</c> or <c>(000) 000-0000</c>.</summary>
    [Parameter, EditorRequired]
    public string Mask { get; set; } = "";

    /// <summary>The committed text (including the literals of the mask), or null when empty or invalid.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised when the value is committed.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>When the value is committed. Defaults to blur or Enter.</summary>
    [Parameter]
    public UrsaMaskCommit CommitOn { get; set; } = UrsaMaskCommit.Blur;

    /// <summary>Upper-cases the letters typed in the mask (for example postal codes).</summary>
    [Parameter]
    public bool UpperCase { get; set; }

    /// <summary>A regular expression the complete value must match, such as <c>^[A-Z]\d[A-Z] \d[A-Z]\d$</c>.</summary>
    [Parameter]
    public string? Pattern { get; set; }

    /// <summary>The error shown when <see cref="Pattern"/> does not match.</summary>
    [Parameter]
    public string PatternError { get; set; } = "Invalid value";

    /// <summary>The error shown when the mask is only partly filled.</summary>
    [Parameter]
    public string IncompleteError { get; set; } = "Incomplete value";

    /// <summary>Extra validation of a complete value: return an error message, or null when it is valid.</summary>
    [Parameter]
    public Func<string, string?>? Validator { get; set; }

    /// <summary>Label of the field.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Variant of the field.</summary>
    [Parameter]
    public Variant Variant { get; set; } = Variant.Text;

    /// <summary>Vertical margin of the field.</summary>
    [Parameter]
    public Margin Margin { get; set; } = Margin.None;

    /// <summary>Placeholder shown when the field is empty.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>Helper text shown under the field.</summary>
    [Parameter]
    public string? HelperText { get; set; }

    /// <summary>Position of the adornment icon.</summary>
    [Parameter]
    public Adornment Adornment { get; set; } = Adornment.None;

    /// <summary>Icon of the adornment.</summary>
    [Parameter]
    public string? AdornmentIcon { get; set; }

    /// <summary>Color of the adornment.</summary>
    [Parameter]
    public Color Color { get; set; } = Color.Default;

    /// <summary>Disables the field.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Makes the field read-only.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>Marks the field as required.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>CSS class of the wrapping element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Inline style of the wrapping element.</summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>Extra attributes passed to the text field.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> UserAttributes { get; set; } = new();

    /// <summary>Called from JavaScript when the value is committed.</summary>
    [JSInvokable]
    public Task OnMaskCommit(string text) => CommitAsync(text);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A value pushed by the parent replaces what is displayed.
        if (Value != _committed)
        {
            _committed = Value;
            _text = Value;
            _error = null;
            _pendingSync = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var key = $"{Mask}|{UpperCase}|{CommitOn}";
        if (_attachedKey == key && !_pendingSync)
        {
            return;
        }

        var attach = _attachedKey is null;
        _attachedKey = key;
        _pendingSync = false;
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _self ??= DotNetObjectReference.Create(this);
        var options = new { mask = Mask, upper = UpperCase, commitOnComplete = CommitOn == UrsaMaskCommit.Complete };
        await _module.InvokeVoidAsync(attach ? "attach" : "update", _wrapper, attach ? _self : options, attach ? options : null);
    }

    // MudTextField's own change event (blur or Enter); it carries the text the script displays.
    private Task OnNativeChangedAsync(string? text) => CommitAsync(text ?? "");

    private async Task CommitAsync(string text)
    {
        var slots = Mask.Count(IsSlot);
        string? value = null;
        _error = null;

        if (text.Length == 0)
        {
            // empty: no value and no error (use Required to forbid it)
        }
        else if (CountFilled(text) < slots)
        {
            _error = IncompleteError;
        }
        else if (Pattern is not null && !Regex.IsMatch(text, Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            _error = PatternError;
        }
        else if (Validator?.Invoke(text) is { } message)
        {
            _error = message;
        }
        else
        {
            value = text;
        }

        _text = text;
        if (value != _committed)
        {
            _committed = value;
            await ValueChanged.InvokeAsync(value);
        }

        StateHasChanged();
    }

    private static bool IsSlot(char c) => c is '0' or 'a' or '*';

    // Number of slot characters in a displayed text: walks the mask the way the script does.
    private int CountFilled(string text)
    {
        var count = 0;
        var m = 0;
        foreach (var ch in text)
        {
            var isLiteral = false;
            while (m < Mask.Length && !IsSlot(Mask[m]))
            {
                if (Mask[m++] == ch)
                {
                    isLiteral = true;
                    break;
                }
            }

            if (isLiteral)
            {
                continue;
            }

            if (m >= Mask.Length)
            {
                break;
            }

            count++;
            m++;
        }

        return count;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("detach", _wrapper);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // the circuit is already gone
        }

        _self?.Dispose();
    }
}
