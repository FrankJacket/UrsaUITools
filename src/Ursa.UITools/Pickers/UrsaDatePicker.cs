using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>
/// A <see cref="MudDatePicker"/> whose text box is editable by default, that can also edit the time of day
/// by typing or by picking it in the popover. Formats follow the culture of the current session
/// (<see cref="MudFormComponent{T, U}.Culture"/>, which defaults to <see cref="CultureInfo.CurrentCulture"/>).
/// </summary>
public class UrsaDatePicker : MudDatePicker
{
    private bool _lastShowTime;
    private CultureInfo? _lastCulture;
    private DateMask? _autoMask;
    private string? _autoMaskText;
    private bool _ownsPickerActions;

    public UrsaDatePicker()
    {
        Editable = true;
    }

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
    /// Applies an input mask built from the culture's date (and time) pattern, for example <c>__/__/____</c>
    /// for fr-FR or <c>__/__/____ __:__ __</c> for en-US with time. A mask passed in <c>Mask</c> always wins,
    /// and an explicit <c>DateFormat</c> only gets a mask when it is already fixed width (such as <c>dd/MM/yyyy</c>).
    /// </summary>
    [Parameter]
    public bool AutoMask { get; set; } = true;

    /// <summary>Minute step of the time picker.</summary>
    [Parameter]
    public int MinuteSelectionStep { get; set; } = 1;

    /// <summary>Text of the button that closes the popover when <see cref="ShowTimePicker"/> is on.</summary>
    [Parameter]
    public string OkText { get; set; } = "OK";

    private bool EffectiveShowTime => ShowTime || ShowTimePicker;

    private CultureInfo EffectiveCulture => GetCulture() ?? CultureInfo.CurrentCulture;

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

        // With the automatic mask, use a fixed-width pattern so formatted text always fits the mask.
        return UsesAutoMask && UrsaDatePattern.TryCreate(pattern, info, out var fixedWidth)
            ? fixedWidth!.Normalized
            : pattern;
    }

    private bool UsesAutoMask => AutoMask && (Mask is null || ReferenceEquals(Mask, _autoMask));

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

        // Re-render the text when something that drives its format or mask changes.
        var culture = EffectiveCulture;
        var maskChanged = UpdateAutoMask();
        if (maskChanged || _lastShowTime != EffectiveShowTime || !ReferenceEquals(_lastCulture, culture))
        {
            _lastShowTime = EffectiveShowTime;
            _lastCulture = culture;
            await DateFormatChangedAsync(base.GetFormat());
        }
    }

    /// <summary>
    /// Keeps <see cref="MudPicker{T}.Mask"/> in sync with the culture format, unless the caller supplied a mask.
    /// </summary>
    /// <returns>True when the automatic mask changed.</returns>
    private bool UpdateAutoMask()
    {
        if (Mask is not null && !ReferenceEquals(Mask, _autoMask))
        {
            return false; // the caller's mask wins
        }

        var text = AutoMask ? ComputeAutoMaskText() : null;
        if (text == _autoMaskText)
        {
            return false;
        }

        _autoMaskText = text;
        _autoMask = text is null ? null : CreateMask(text);
        Mask = _autoMask;
        return true;
    }

    // MudBlazor's built-in DateMask, with a visible placeholder and upper-cased AM/PM.
    private static DateMask CreateMask(string text) => new(text) { Placeholder = '_', Transformation = char.ToUpperInvariant };

    private string? ComputeAutoMaskText()
    {
        var info = EffectiveCulture.DateTimeFormat;
        var explicitFormat = base.GetFormat() != info.ShortDatePattern;
        if (!UrsaDatePattern.TryCreate(GetFormat(), info, out var pattern))
        {
            return null;
        }

        // An explicit DateFormat is never padded, so it only gets a mask when it is already fixed width.
        return explicitFormat && !pattern!.IsFixedWidth ? null : pattern!.MaskText;
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
}
