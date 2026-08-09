using System.Windows.Forms;

namespace MouseCursorSupporter.Core;

/// <summary>
/// Drives automatic cursor switching: fixed interval, time-of-day table, and the
/// switch-on-startup option. Manual switches (from the tray menu) go through the same
/// ApplyPackDirect/SwitchNow entry points so state stays consistent.
/// </summary>
public sealed class SchedulerEngine : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _nextIntervalDueUtc = DateTime.MaxValue;
    private int _lastAppliedSlotStartMinutes = -1;

    public event Action<CursorPack>? PackApplied;

    public SchedulerEngine(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;
        _timer = new System.Windows.Forms.Timer { Interval = 20_000 };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        if (_settings.Schedule.SwitchOnStartup)
        {
            RunStartupSwitch();
        }

        ResetIntervalDueTime();
        _timer.Start();
        // Evaluate immediately so a TimeOfDay schedule takes effect right away rather than
        // waiting for the first tick.
        Tick();
    }

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Dispose();

    /// <summary>Called when the user edits schedule settings, so timers reflect the new config.</summary>
    public void OnSettingsChanged() => ResetIntervalDueTime();

    private void ResetIntervalDueTime()
    {
        var minutes = Math.Max(1, _settings.Schedule.IntervalMinutes);
        _nextIntervalDueUtc = DateTime.UtcNow.AddMinutes(minutes);
    }

    private void RunStartupSwitch()
    {
        if (_settings.Schedule.Mode == ScheduleMode.TimeOfDay)
        {
            ApplyCurrentTimeSlotIfAny(force: true);
        }
        else
        {
            SwitchNow();
        }
    }

    private void Tick()
    {
        switch (_settings.Schedule.Mode)
        {
            case ScheduleMode.Interval:
                if (DateTime.UtcNow >= _nextIntervalDueUtc)
                {
                    SwitchNow();
                    ResetIntervalDueTime();
                }
                break;

            case ScheduleMode.TimeOfDay:
                ApplyCurrentTimeSlotIfAny(force: false);
                break;

            case ScheduleMode.Manual:
            default:
                break;
        }
    }

    private void ApplyCurrentTimeSlotIfAny(bool force)
    {
        var slots = _settings.Schedule.TimeSlots;
        if (slots.Count == 0)
        {
            return;
        }

        var nowMinutes = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        // The active slot is the one with the latest StartMinutes <= now; if none qualify
        // (now is before the earliest slot), wrap around to the last slot of the previous day.
        var ordered = slots.OrderBy(s => s.StartMinutes).ToList();
        var slot = ordered.LastOrDefault(s => s.StartMinutes <= nowMinutes) ?? ordered[^1];

        if (!force && slot.StartMinutes == _lastAppliedSlotStartMinutes)
        {
            return;
        }

        var pack = _settings.Packs.FirstOrDefault(p => p.Id == slot.PackId);
        if (pack is null)
        {
            return;
        }

        _lastAppliedSlotStartMinutes = slot.StartMinutes;
        ApplyPackDirect(pack);
    }

    /// <summary>Picks the next pack per the configured selection mode and applies it.</summary>
    public void SwitchNow()
    {
        var pool = GetCandidatePool();
        if (pool.Count == 0)
        {
            return;
        }

        CursorPack next;
        if (_settings.Schedule.SelectionMode == CursorSelectionMode.Random)
        {
            next = pool.Count == 1
                ? pool[0]
                : PickRandomExcluding(pool, _settings.ActivePackId);
        }
        else
        {
            var nextIndex = (_settings.Schedule.LastSequentialIndex + 1) % pool.Count;
            _settings.Schedule.LastSequentialIndex = nextIndex;
            next = pool[nextIndex];
        }

        ApplyPackDirect(next);
    }

    private static CursorPack PickRandomExcluding(List<CursorPack> pool, string? excludeId)
    {
        var choices = pool.Where(p => p.Id != excludeId).ToList();
        if (choices.Count == 0)
        {
            choices = pool;
        }
        return choices[Random.Shared.Next(choices.Count)];
    }

    private List<CursorPack> GetCandidatePool()
    {
        var listId = _settings.Schedule.ActiveListId;
        if (!string.IsNullOrEmpty(listId))
        {
            var list = _settings.Lists.FirstOrDefault(l => l.Id == listId);
            if (list is not null && list.PackIds.Count > 0)
            {
                return _settings.Packs.Where(p => list.PackIds.Contains(p.Id)).ToList();
            }
        }

        return [.. _settings.Packs];
    }

    /// <summary>Applies a specific pack, e.g. from a manual tray menu selection.</summary>
    public void ApplyPackDirect(CursorPack pack)
    {
        CursorRegistryService.ApplyPack(pack);
        _settings.ActivePackId = pack.Id;
        _saveSettings();
        PackApplied?.Invoke(pack);
    }
}
