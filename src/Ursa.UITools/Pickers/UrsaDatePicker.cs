using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>
/// A <see cref="MudDatePicker"/> whose text box is editable by default, with an input mask built from the culture
/// of the session and that runs in the browser (see <see cref="UrsaMaskedField"/>): the server only hears about the
/// text when it is complete, on blur or on Enter, so typing never lags or loses the caret, and typing in the middle of
/// a date overwrites instead of erasing it. It can also edit the time of day, by typing or with a time picker in the
/// popover. A date that is incomplete, does not exist or is outside <c>MinDate</c>/<c>MaxDate</c> is reported as an error.
/// </summary>
public class UrsaDatePicker : MudDatePicker
{
    private const string ModulePath = "./_content/Ursa.UITools/Inputs/UrsaMaskedField.razor.js";

    private bool _lastShowTime;
    private CultureInfo? _lastCulture;
    private bool _ownsPickerActions;
    private string? _jsKey;
    private IJSObjectReference? _module;
    private DotNetObjectReference<UrsaDatePicker>? _self;

    public UrsaDatePicker()
    {
        Editable = true;
    }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// Also edits the time of day by typing. The text uses the culture's short date pattern followed by its
    /// short time pattern, unless <c>DateFormat</c> is set to something else.
    /// </summary>
    [Parameter]
    public bool ShowTime { get; set; }

    /// <summary>
    /// Shows a time picker next to the calendar in the popover. Implies <see cref="ShowTime"/>.
    /// Changes are applied immediately; use the OK button, Escape or click outside to close.
    /// </summary>
    [Parameter]
    public bool ShowTimePicker { get; set; }

    /// <summary>
    /// Applies an input mask built from the culture's date (and time) pattern, for example <c>00/00/0000</c>
    /// for fr-FR or <c>00/00/0000 00:00 aa</c> for en-US with time. An explicit <c>DateFormat</c> only gets a mask
    /// when it is already fixed width (such as <c>dd/MM/yyyy</c>).
    /// </summary>
    [Parameter]
    public bool AutoMask { get; set; } = true;

    /// <summary>
    /// Your own mask (<c>0</c> is a digit, <c>a</c> a letter, anything else is a literal), used instead of the one
    /// built from the culture. Set <c>DateFormat</c> to the matching .NET format, for example
    /// <c>MaskText="0000-00-00" DateFormat="yyyy-MM-dd"</c>.
    /// </summary>
    [Parameter]
    public string? MaskText { get; set; }

    /// <summary>Minute step of the time picker.</summary>
    [Parameter]
    public int MinuteSelectionStep { get; set; } = 1;

    /// <summary>Text of the button that closes the popover when <see cref="ShowTimePicker"/> is on.</summary>
    [Parameter]
    public string OkText { get; set; } = "OK";

    private bool EffectiveShowTime => ShowTime || ShowTimePicker;

    private CultureInfo EffectiveCulture => GetCulture() ?? CultureInfo.CurrentCulture;

    private string? ActiveMaskText => AutoMask && Mask is null ? ComputeMaskText() : null;

    /// <inheritdoc />
    protected override string GetFormat()
    {
        // base.GetFormat() returns DateFormat when set, otherwise the culture's short date pattern.
        var dateFormat = base.GetFormat();
        var info = EffectiveCulture.DateTimeFormat;
        if (dateFormat != info.ShortDatePattern)
        {
            return dateFormat; // an explicit DateFormat wins
        }

        var pattern = EffectiveShowTime ? $"{info.ShortDatePattern} {info.ShortTimePattern}" : info.ShortDatePattern;

        // With the mask, use a fixed-width pattern so formatted text always fits it.
        return AutoMask && Mask is null && UrsaDatePattern.TryCreate(pattern, info, out var fixedWidth)
            ? fixedWidth!.Normalized
            : pattern;
    }

    private string? ComputeMaskText()
    {
        if (!string.IsNullOrEmpty(MaskText))
        {
            return MaskText;
        }

        var info = EffectiveCulture.DateTimeFormat;
        var explicitFormat = base.GetFormat() != info.ShortDatePattern;
        if (!UrsaDatePattern.TryCreate(GetFormat(), info, out var pattern))
        {
            return null;
        }

        // An explicit DateFormat is never padded, so it only gets a mask when it is already fixed width.
        return explicitFormat && !pattern!.IsFixedWidth
            ? null
            : pattern!.MaskText.Replace('y', '0').Replace('M', '0').Replace('d', '0');
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        InputId ??= $"ursa-date-{Guid.NewGuid():N}"; // the script finds the input by id
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (ShowTimePicker && PickerActions is null)
        {
            _ownsPickerActions = true;
            PickerActions = context => builder =>
            {
                builder.OpenComponent<MudButton>(0);
                builder.AddComponentParameter(1, nameof(MudButton.Color), Color);
                builder.AddComponentParameter(2, nameof(MudButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => context.CloseAsync()));
                builder.AddComponentParameter(3, nameof(MudButton.ChildContent), (RenderFragment)(content => content.AddContent(4, OkText)));
                builder.CloseComponent();
            };
        }
        else if (!ShowTimePicker && _ownsPickerActions)
        {
            _ownsPickerActions = false;
            PickerActions = null;
        }

        // The date must be inside MinDate and MaxDate; the converter already rejects text that is not a date.
        if (Validation is null)
        {
            Validation = (Func<DateTime?, string?>)ValidateRange;
        }

        // Re-render the text when something that drives its format changes.
        var culture = EffectiveCulture;
        if (_lastShowTime != EffectiveShowTime || !ReferenceEquals(_lastCulture, culture))
        {
            _lastShowTime = EffectiveShowTime;
            _lastCulture = culture;
            await DateFormatChangedAsync(base.GetFormat());
        }
    }

    // MinDate and MaxDate are read straight from the parameters: the ParameterState instances are private to MudBlazor.
#pragma warning disable MUD0010
    private string? ValidateRange(DateTime? date)
    {
        if (date is not { } value)
        {
            return null;
        }

        var info = EffectiveCulture.DateTimeFormat;
        if (MinDate is { } min && value.Date < min.Date)
        {
            return $"The date must be on or after {min.ToString(info.ShortDatePattern, EffectiveCulture)}";
        }

        if (MaxDate is { } max && value.Date > max.Date)
        {
            return $"The date must be on or before {max.ToString(info.ShortDatePattern, EffectiveCulture)}";
        }

        return null;
    }
#pragma warning restore MUD0010


    /// <summary>Called from JavaScript when the masked text is complete, or on blur or Enter.</summary>
    [JSInvokable]
    public async Task OnMaskCommit(string text)
    {
        await SetTextAsync(text, true);
        StateHasChanged();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        var mask = ActiveMaskText;
        if (mask == _jsKey)
        {
            return;
        }

        var attach = _jsKey is null;
        _jsKey = mask;
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);
            if (mask is null)
            {
                await _module.InvokeVoidAsync("detachById", InputId);
                return;
            }

            var options = new { mask, upper = true, commitOnComplete = true };
            if (attach)
            {
                await _module.InvokeVoidAsync("attachById", InputId, _self, options);
            }
            else
            {
                await _module.InvokeVoidAsync("updateById", InputId, options);
            }
        }
        catch (JSDisconnectedException)
        {
            // the circuit is already gone
        }
    }

    /// <inheritdoc />
    protected override Task OnDayClickedAsync(DateTime dateTime)
    {
        if (!EffectiveShowTime)
        {
            return base.OnDayClickedAsync(dateTime);
        }

        var merged = WithTime(dateTime, Date?.TimeOfDay ?? TimeSpan.Zero);
        return ShowTimePicker
            ? SetDateAsync(merged, true)
            : base.OnDayClickedAsync(merged);
    }

    /// <inheritdoc />
    protected override Task SubmitAsync() =>
        ShowTimePicker ? Task.CompletedTask : base.SubmitAsync(); // the value is applied live

    /// <inheritdoc />
    protected override RenderFragment PickerContent => builder =>
    {
        if (!ShowTimePicker)
        {
            builder.AddContent(0, base.PickerContent);
            return;
        }

        // Date part: MudBlazor's own container stacks the toolbar above the calendar.
        // Both parts wrap onto two rows on narrow screens.
        builder.OpenElement(1, "div");
        builder.AddAttribute(2, "class", "d-flex flex-wrap");
        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "mud-picker-container");
        builder.AddContent(5, base.PickerContent);
        builder.CloseElement();

        builder.OpenComponent<MudTimePicker>(6);
        builder.AddComponentParameter(7, nameof(MudTimePicker.PickerVariant), PickerVariant.Static);
        builder.AddComponentParameter(8, nameof(MudTimePicker.Elevation), 0);
        builder.AddComponentParameter(9, nameof(MudTimePicker.Color), Color);
        builder.AddComponentParameter(10, nameof(MudTimePicker.AmPm), UsesAmPm);
        builder.AddComponentParameter(11, nameof(MudTimePicker.MinuteSelectionStep), MinuteSelectionStep);
        builder.AddComponentParameter(12, nameof(MudTimePicker.Time), Date?.TimeOfDay);
        builder.AddComponentParameter(13, nameof(MudTimePicker.TimeChanged),
            EventCallback.Factory.Create<TimeSpan?>(this, OnTimeChangedAsync));
        builder.CloseComponent();

        builder.CloseElement();
    };

    private bool UsesAmPm => EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains("tt", StringComparison.Ordinal);

    private Task OnTimeChangedAsync(TimeSpan? time) =>
        SetDateAsync(WithTime(Date ?? DateTime.Today, time ?? TimeSpan.Zero), true);

    private static DateTime WithTime(DateTime date, TimeSpan time) =>
        DateTime.SpecifyKind(date.Date + time, DateTimeKind.Unspecified);

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("detachById", InputId);
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
