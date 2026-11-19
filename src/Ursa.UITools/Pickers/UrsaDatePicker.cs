using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>
/// An editable <see cref="MudDatePicker"/> without an input mask (so nothing is sent to the server while typing) that
/// shows and reads dates in the culture of the session (fr-CA: <c>2026-01-02</c>; the <c>Culture</c> parameter
/// overrides it).
/// <para>
/// A <see cref="UrsaFlexibleDateConverter"/> converts the text when the field is left: you can type <c>20260102</c>,
/// <c>260102</c>, <c>2026/1/2</c> or <c>2026.01.02</c> and the field shows <c>2026-01-02</c>. A date that does not exist
/// (<c>20260231</c>) is refused with a message. A validation rule also checks <c>MinDate</c> and <c>MaxDate</c>.
/// </para>
/// <para>
/// With <see cref="ShowTime"/> the time can be typed too (<c>202601021430</c>, <c>2026-01-02 14:30</c>); with
/// <see cref="ShowTimePicker"/> a time picker is added next to the calendar. The "Today" button of the calendar picks
/// the current date. The text always uses the culture's short date (and time) patterns: <c>DateFormat</c> is not used.
/// </para>
/// </summary>
public class UrsaDatePicker : MudDatePicker
{
    private static readonly UrsaLoc L = new();

    private readonly UrsaFlexibleDateConverter _converter;

    public UrsaDatePicker()
    {
        _converter = new UrsaFlexibleDateConverter(() => EffectiveShowTime, () => EffectiveCulture);
        Editable = true;
        AutoClose = true; // with PickerActions, a click on a day no longer commits without this
        Converter = _converter;
        TextChanged = EventCallback.Factory.Create<string?>(this, NormalizeTextAsync);
        PickerActions = context => builder =>
        {
            builder.OpenComponent<MudButton>(0);
            builder.AddAttribute(1, nameof(MudButton.Variant), Variant.Text);
            builder.AddAttribute(2, nameof(MudButton.Color), Color.Primary);
            builder.AddAttribute(3, nameof(MudButton.Size), Size.Small);
            builder.AddAttribute(4, nameof(MudButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, GoToTodayAsync));
            builder.AddAttribute(5, nameof(MudButton.ChildContent), (RenderFragment)(b => b.AddContent(0, L["Today"])));
            builder.CloseComponent();

            if (ShowTimePicker)
            {
                // The time is applied live: this button simply closes the calendar.
                builder.OpenComponent<MudButton>(10);
                builder.AddAttribute(11, nameof(MudButton.Variant), Variant.Text);
                builder.AddAttribute(12, nameof(MudButton.Color), Color.Primary);
                builder.AddAttribute(13, nameof(MudButton.Size), Size.Small);
                builder.AddAttribute(14, nameof(MudButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => context.CloseAsync()));
                builder.AddAttribute(15, nameof(MudButton.ChildContent), (RenderFragment)(b => b.AddContent(0, L["OK"])));
                builder.CloseComponent();
            }
        };
    }

    /// <summary>Also allows typing the time (the culture's short time pattern, after the date).</summary>
    [Parameter]
    public bool ShowTime { get; set; }

    /// <summary>Adds a time picker next to the calendar. Implies <see cref="ShowTime"/>.</summary>
    [Parameter]
    public bool ShowTimePicker { get; set; }

    /// <summary>Minute step of the time picker.</summary>
    [Parameter]
    public int MinuteSelectionStep { get; set; } = 1;

    private bool EffectiveShowTime => ShowTime || ShowTimePicker;

    private CultureInfo EffectiveCulture => GetCulture() ?? CultureInfo.CurrentCulture;

    private async Task GoToTodayAsync()
    {
        var today = DateTime.Today;
        if (!EffectiveShowTime)
        {
            await OnDayClickedAsync(today);
            await CloseAsync();
            return;
        }

        // Keep the time already chosen; otherwise use the current time.
        var time = Date?.TimeOfDay ?? TimeSpan.FromMinutes((int)DateTime.Now.TimeOfDay.TotalMinutes);
        await SetDateAsync(WithTime(today, time), true);
        if (!ShowTimePicker)
        {
            await CloseAsync();
        }
    }

    // After a valid free-form entry (20260102), replaces the typed text with the culture's format (2026-01-02).
    private async Task NormalizeTextAsync(string? typed)
    {
        await Task.Yield();
        if (Date is not { } date)
        {
            return;
        }

        var formatted = _converter.Convert(date);
        if (formatted != Text)
        {
            Text = formatted;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        Validation ??= (Func<DateTime?, string?>)ValidateRange;
        ShowToolbar = false;

        return base.OnParametersSetAsync();
    }

    // MinDate and MaxDate are read straight from the parameters: the ParameterState instances are private to MudBlazor.
#pragma warning disable MUD0010
    private string? ValidateRange(DateTime? date) => UrsaDateRules.Validate(date, MinDate, MaxDate, EffectiveCulture);
#pragma warning restore MUD0010

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

        // The calendar and the time picker wrap onto two rows on narrow screens.
        builder.OpenElement(1, "div");
        builder.AddAttribute(2, "class", "d-flex flex-wrap");
        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "mud-picker-container");
        builder.AddContent(5, base.PickerContent);
        builder.CloseElement();

        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "d-flex flex-column align-center");

        builder.OpenComponent<MudTimePicker>(8);
        builder.AddComponentParameter(9, nameof(MudTimePicker.PickerVariant), PickerVariant.Static);
        builder.AddComponentParameter(10, nameof(MudTimePicker.Elevation), 0);
        builder.AddComponentParameter(11, nameof(MudTimePicker.Color), Color);
        builder.AddComponentParameter(12, nameof(MudTimePicker.AmPm), UsesAmPm);
        builder.AddComponentParameter(13, nameof(MudTimePicker.MinuteSelectionStep), MinuteSelectionStep);
        builder.AddComponentParameter(14, nameof(MudTimePicker.Time), Date?.TimeOfDay);
        builder.AddComponentParameter(15, nameof(MudTimePicker.TimeChanged),
            EventCallback.Factory.Create<TimeSpan?>(this, OnTimeChangedAsync));
        builder.AddComponentParameter(16, nameof(MudTimePicker.ShowToolbar), ShowToolbar);
        builder.CloseComponent();

        // The AM/PM switch of MudTimePicker lives in its toolbar. Without the toolbar, show one under the clock.
        if (UsesAmPm && !ShowToolbar)
        {
            builder.AddContent(17, AmPmSelector);
        }

        builder.CloseElement();

        builder.CloseElement();
    };

    private bool UsesAmPm => EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains("tt", StringComparison.Ordinal);

    private bool IsPm => Date?.Hour >= 12;

    // AM and PM as the culture writes them ("AM"/"PM" in en-US).
    private string AmText => FirstNotEmpty(EffectiveCulture.DateTimeFormat.AMDesignator, "AM");

    private string PmText => FirstNotEmpty(EffectiveCulture.DateTimeFormat.PMDesignator, "PM");

    private static string FirstNotEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private RenderFragment AmPmSelector => builder =>
    {
        builder.OpenComponent<MudToggleGroup<bool>>(0);
        builder.AddComponentParameter(1, nameof(MudToggleGroup<bool>.Value), IsPm);
        builder.AddComponentParameter(2, nameof(MudToggleGroup<bool>.ValueChanged), EventCallback.Factory.Create<bool>(this, OnAmPmChangedAsync));
        builder.AddComponentParameter(3, nameof(MudToggleGroup<bool>.Color), Color);
        builder.AddComponentParameter(4, nameof(MudToggleGroup<bool>.Size), Size.Small);
        builder.AddComponentParameter(5, nameof(MudToggleGroup<bool>.Class), "mb-2");
        builder.AddComponentParameter(6, nameof(MudToggleGroup<bool>.ChildContent), (RenderFragment)(items =>
        {
            items.OpenComponent<MudToggleItem<bool>>(0);
            items.AddComponentParameter(1, nameof(MudToggleItem<bool>.Value), false);
            items.AddComponentParameter(2, nameof(MudToggleItem<bool>.Text), AmText);
            items.CloseComponent();

            items.OpenComponent<MudToggleItem<bool>>(3);
            items.AddComponentParameter(4, nameof(MudToggleItem<bool>.Value), true);
            items.AddComponentParameter(5, nameof(MudToggleItem<bool>.Text), PmText);
            items.CloseComponent();
        }));
        builder.CloseComponent();
    };

    // Moving between AM and PM shifts the time by 12 hours and leaves the date alone.
    private Task OnAmPmChangedAsync(bool pm)
    {
        var current = Date ?? DateTime.Today;
        if (pm == current.Hour >= 12)
        {
            return Task.CompletedTask;
        }

        return SetDateAsync(WithTime(current, current.TimeOfDay + TimeSpan.FromHours(pm ? 12 : -12)), true);
    }

    private Task OnTimeChangedAsync(TimeSpan? time) =>
        SetDateAsync(WithTime(Date ?? DateTime.Today, time ?? TimeSpan.Zero), true);

    private static DateTime WithTime(DateTime date, TimeSpan time) =>
        DateTime.SpecifyKind(date.Date + time, DateTimeKind.Unspecified);
}
