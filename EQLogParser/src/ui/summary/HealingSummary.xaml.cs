﻿
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EQLogParser
{
  /// <summary>
  /// Interaction logic for HealSummary.xaml
  /// </summary>
  public partial class HealingSummary : SummaryTable, IDisposable
  {
    private string CurrentClass = null;

    public HealingSummary()
    {
      InitializeComponent();
      InitSummaryTable(title, dataGrid);

      var list = PlayerManager.Instance.GetClassList();
      list.Insert(0, "All Classes");
      classesList.ItemsSource = list;
      classesList.SelectedIndex = 0;

      CreateClassMenuItems(menuItemShowSpellCasts, DataGridShowSpellCastsClick, DataGridSpellCastsByClassClick);
      CreateClassMenuItems(menuItemShowBreakdown, DataGridShowBreakdownClick, DataGridShowBreakdownByClassClick);

      HealingStatsManager.Instance.EventsGenerationStatus += Instance_EventsGenerationStatus;
      DataManager.Instance.EventsClearedActiveData += Instance_EventsClearedActiveData;
    }

    private void Instance_EventsClearedActiveData(object sender, bool cleared)
    {
      CurrentStats = null;
      dataGrid.ItemsSource = null;
      title.Content = DEFAULT_TABLE_LABEL;
    }

    private void Instance_EventsGenerationStatus(object sender, StatsGenerationEvent e)
    {
      Dispatcher.InvokeAsync(() =>
      {
        switch (e.State)
        {
          case "STARTED":
            (Application.Current.MainWindow as MainWindow).Busy(true);
            title.Content = "Calculating HPS...";
            dataGrid.ItemsSource = null;
            break;
          case "COMPLETED":
            CurrentStats = e.CombinedStats as CombinedStats;
            CurrentGroups = e.Groups;

            if (CurrentStats == null)
            {
              title.Content = NODATA_TABLE_LABEL;
            }
            else
            {
              title.Content = CurrentStats.FullTitle;
              var view = CollectionViewSource.GetDefaultView(CurrentStats.StatsList);
              dataGrid.ItemsSource = SetFilter(view);
            }

            if (!MainWindow.IsAoEHealingEnabled)
            {
              title.Content += " (Not Including AE Healing)";
            }

            (Application.Current.MainWindow as MainWindow).Busy(false);
            UpdateDataGridMenuItems();
            break;
          case "NONPC":
          case "NODATA":
            CurrentStats = null;
            title.Content = e.State == "NONPC" ? DEFAULT_TABLE_LABEL : NODATA_TABLE_LABEL;
            (Application.Current.MainWindow as MainWindow).Busy(false);
            UpdateDataGridMenuItems();
            break;
        }
      });
    }

    internal void DataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      FireSelectionChangedEvent(GetSelectedStats());
      UpdateDataGridMenuItems();
    }

    internal override void ShowBreakdown(List<PlayerStats> selected)
    {
      if (selected?.Count > 0)
      {
        var main = Application.Current.MainWindow as MainWindow;
        var healTable = new HealBreakdown(CurrentStats);
        healTable.Show(selected);
        Helpers.OpenNewTab(main.dockSite, "healWindow", "Healing Breakdown", healTable);
      }
    }

    private void DataGridHealingLogClick(object sender, RoutedEventArgs e)
    {
      if (dataGrid.SelectedItems.Count == 1)
      {
        var log = new HitLogViewer(CurrentStats, dataGrid.SelectedItems.Cast<PlayerStats>().First(), CurrentGroups);
        var main = Application.Current.MainWindow as MainWindow;
        var window = Helpers.OpenNewTab(main.dockSite, "healingLog", "Healing Log", log, 400, 300);
        window.CanFloat = true;
        window.CanClose = true;
      }
    }

    private void UpdateDataGridMenuItems()
    {
      if (CurrentStats != null && CurrentStats.StatsList.Count > 0)
      {
        menuItemSelectAll.IsEnabled = dataGrid.SelectedItems.Count < dataGrid.Items.Count;
        menuItemUnselectAll.IsEnabled = dataGrid.SelectedItems.Count > 0;
        menuItemShowBreakdown.IsEnabled = menuItemShowSpellCasts.IsEnabled = true;
        menuItemShowHealingLog.IsEnabled = dataGrid.SelectedItems.Count == 1;
        copyHealParseToEQClick.IsEnabled = true;
        EnableClassMenuItems(menuItemShowBreakdown, dataGrid, CurrentStats.UniqueClasses);
        EnableClassMenuItems(menuItemShowSpellCasts, dataGrid, CurrentStats.UniqueClasses);
      }
      else
      {
        menuItemUnselectAll.IsEnabled = menuItemSelectAll.IsEnabled = menuItemShowBreakdown.IsEnabled =
          menuItemShowHealingLog.IsEnabled = menuItemShowSpellCasts.IsEnabled = copyHealParseToEQClick.IsEnabled = false;
      }
    }

    private ICollectionView SetFilter(ICollectionView view)
    {
      if (view != null)
      {
        view.Filter = (stats) =>
        {
          string className = null;
          if (stats is PlayerStats playerStats)
          {
            className = playerStats.ClassName;
          }
          else if (stats is DataPoint dataPoint)
          {
            className = PlayerManager.Instance.GetPlayerClass(dataPoint.Name);
          }

          return string.IsNullOrEmpty(CurrentClass) || CurrentClass == className;
        };

        HealingStatsManager.Instance.FireFilterEvent(new GenerateStatsOptions() { RequestChartData = true }, view.Filter);
      }

      return view;
    }

    private void CopyToEQClick(object sender, RoutedEventArgs e)
    {
      (Application.Current.MainWindow as MainWindow).CopyToEQClick(Labels.HEALPARSE);
    }

    private void ListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CurrentClass = classesList.SelectedIndex <= 0 ? null : classesList.SelectedValue.ToString();
      SetFilter(dataGrid?.ItemsSource as ICollectionView);
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects).
          CurrentStats = null;
        }

        HealingStatsManager.Instance.EventsGenerationStatus -= Instance_EventsGenerationStatus;
        DataManager.Instance.EventsClearedActiveData -= Instance_EventsClearedActiveData;
        disposedValue = true;
      }
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
      // TODO: uncomment the following line if the finalizer is overridden above.
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
