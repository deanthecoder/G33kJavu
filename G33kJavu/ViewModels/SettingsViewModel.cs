// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.ViewModels;
using G33kJavu.Core.Models;

namespace G33kJavu.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ScanSettings m_settings;

    public SettingsViewModel(ScanSettings settings)
    {
        m_settings = settings;
    }

    public int K
    {
        get => m_settings.K;
        set
        {
            if (m_settings.K == value)
                return;
            m_settings.K = value;
            OnPropertyChanged();
        }
    }

    public int W
    {
        get => m_settings.W;
        set
        {
            if (m_settings.W == value)
                return;
            m_settings.W = value;
            OnPropertyChanged();
        }
    }

    public int MinReportLines
    {
        get => m_settings.MinReportLines;
        set
        {
            if (m_settings.MinReportLines == value)
                return;
            m_settings.MinReportLines = value;
            OnPropertyChanged();
        }
    }

    public int GapAllowance
    {
        get => m_settings.GapAllowance;
        set
        {
            if (m_settings.GapAllowance == value)
                return;
            m_settings.GapAllowance = value;
            OnPropertyChanged();
        }
    }

    public int MaxOccurrencesPerFingerprint
    {
        get => m_settings.MaxOccurrencesPerFingerprint;
        set
        {
            if (m_settings.MaxOccurrencesPerFingerprint == value)
                return;
            m_settings.MaxOccurrencesPerFingerprint = value;
            OnPropertyChanged();
        }
    }
}

