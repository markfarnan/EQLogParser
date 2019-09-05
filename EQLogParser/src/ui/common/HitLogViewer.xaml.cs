﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EQLogParser
{
  /// <summary>
  /// Interaction logic for DamageLog.xaml
  /// </summary>
  public partial class HitLogViewer : UserControl
  {
    private readonly object CollectionLock = new object();
    private ObservableCollection<HitLogRow> Records = new ObservableCollection<HitLogRow>();
    private ObservableCollection<string> Actions = new ObservableCollection<string>();
    private ObservableCollection<string> Types = new ObservableCollection<string>();

    private string ActedOption = "Unknown";
    private List<List<ActionBlock>> CurrentGroups;
    private bool Defending;
    private PlayerStats PlayerStats;

    private string CurrentActedFilter = null;
    private string CurrentActionFilter = null;
    private string CurrentTypeFilter = null;
    private bool CurrentShowPetsFilter = true;
    private bool CurrentGroupSpellCastsFilter = true;

    public HitLogViewer(CombinedStats currentStats, PlayerStats playerStats, List<List<ActionBlock>> groups, bool defending = false)
    {
      InitializeComponent();

      CurrentGroups = groups;
      Defending = defending;
      PlayerStats = playerStats;

      titleLabel.Content = currentStats?.ShortTitle;
      var view = CollectionViewSource.GetDefaultView(Records);

      view.Filter = new Predicate<object>(item =>
      {
        var record = (HitLogRow)item;
        return (string.IsNullOrEmpty(CurrentTypeFilter) || CurrentTypeFilter == record.Type) &&
        (string.IsNullOrEmpty(CurrentActionFilter) || CurrentActionFilter == record.SubType) &&
        (string.IsNullOrEmpty(CurrentActedFilter) || CurrentActedFilter == record.Acted) &&
        (CurrentShowPetsFilter || !record.IsPet);
      });

      BindingOperations.EnableCollectionSynchronization(Records, CollectionLock);
      BindingOperations.EnableCollectionSynchronization(Actions, CollectionLock);
      BindingOperations.EnableCollectionSynchronization(Types, CollectionLock);

      var firstAction = groups?.First()?.First()?.Actions?.First();
      if (firstAction is DamageRecord && !defending)
      {
        ActedOption = "All Defenders";
        dataGrid.Columns[3].Header = "Damage";
        dataGrid.Columns[9].Header = "Attacker";
        dataGrid.Columns[10].Header = "Defender";
        showPets.Visibility = Visibility.Visible;
      }
      else if (firstAction is DamageRecord && defending)
      {
        ActedOption = "All Attackers";
        dataGrid.Columns[3].Header = "Damage";
        dataGrid.Columns[6].Visibility = Visibility.Collapsed;
        dataGrid.Columns[7].Visibility = Visibility.Collapsed;
        dataGrid.Columns[8].Visibility = Visibility.Collapsed;
        dataGrid.Columns[9].Header = "Defender";
        dataGrid.Columns[10].Header = "Attacker";
        showPets.Visibility = Visibility.Collapsed;
        petDivider.Visibility = Visibility.Collapsed;
      }
      else if (firstAction is HealRecord)
      {
        ActedOption = "All Healed Players";
        dataGrid.Columns[3].Header = "Heal";
        dataGrid.Columns[4].Visibility = Visibility.Visible;
        dataGrid.Columns[9].Header = "Healer";
        dataGrid.Columns[10].Header = "Healed";
        showPets.Visibility = Visibility.Collapsed;
        petDivider.Visibility = Visibility.Collapsed;
      }

      actionList.ItemsSource = Actions;
      typeList.ItemsSource = Types;
      dataGrid.ItemsSource = view;

      Actions.Add("All Actions");
      Types.Add("All Types");
      actionList.SelectedIndex = 0;
      typeList.SelectedIndex = 0;

      Display(true);
    }

    private void Display(bool init = false)
    {
      if (init || actionList.IsEnabled)
      {
        actionList.IsEnabled = typeList.IsEnabled = actedList.IsEnabled = showPets.IsEnabled = groupHits.IsEnabled = false;
        dataGrid.Columns[5].Visibility = CurrentGroupSpellCastsFilter ? Visibility.Visible : Visibility.Collapsed;

        Task.Delay(125).ContinueWith(task =>
        {
          Helpers.SetBusy(true);
          var rowCache = new Dictionary<string, HitLogRow>();
          Dictionary<string, byte> uniqueDefenders = new Dictionary<string, byte>();
          Dictionary<string, byte> uniqueActions = new Dictionary<string, byte>();
          Dictionary<string, byte> uniqueTypes = new Dictionary<string, byte>();

          ObservableCollection<string> acted = new ObservableCollection<string>
          {
            ActedOption
          };

          lock (CollectionLock)
          {
            Records.Clear();
          }

          CurrentGroups?.ForEach(group =>
          {
            group.ForEach(block =>
            {
              rowCache.Clear();
              block.Actions.ForEach(action =>
              {
                if (CreateRow(rowCache, PlayerStats, action, block.BeginTime, Defending) is HitLogRow row && !CurrentGroupSpellCastsFilter)
                {
                  AddRow(row, uniqueActions, uniqueDefenders, uniqueTypes, acted);
                }
              });

              if (CurrentGroupSpellCastsFilter)
              {
                foreach (var row in rowCache.Values.OrderByDescending(row => row.Total))
                {
                  AddRow(row, uniqueActions, uniqueDefenders, uniqueTypes, acted);
                }
              }
            });
          });

          Dispatcher.InvokeAsync(() =>
          {
            actedList.ItemsSource = acted;

            if (CurrentActedFilter == null)
            {
              actedList.SelectedIndex = 0;
            }
            else if (acted.IndexOf(CurrentActedFilter) is int actedIndex && actedIndex > -1)
            {
              actedList.SelectedIndex = actedIndex;
            }
            else
            {
              CurrentActedFilter = null;
              actedList.SelectedIndex = 0;
            }

            actionList.IsEnabled = typeList.IsEnabled = actedList.IsEnabled = showPets.IsEnabled = groupHits.IsEnabled = true;
          });

          Helpers.SetBusy(false);
        }, TaskScheduler.Default);
      }
    }

    private void AddRow(HitLogRow row, Dictionary<string, byte> uniqueActions, Dictionary<string, byte> uniqueDefenders, Dictionary<string, byte> uniqueTypes, ObservableCollection<string> acted)
    {
      lock (CollectionLock)
      {
        Records.Add(row);
      }

      PopulateOption(uniqueActions, row.SubType, Actions);
      PopulateOption(uniqueDefenders, row.Acted, acted);
      PopulateOption(uniqueTypes, row.Type, Types);
    }

    private void PopulateOption(Dictionary<string, byte> cache, string value, ObservableCollection<string> list)
    {
      if (!string.IsNullOrEmpty(value) && !cache.ContainsKey(value))
      {
        cache[value] = 1;

        lock (CollectionLock)
        {
          if (list.Count == 1)
          {
            list.Insert(1, value);
          }
          else
          {
            int i = 1;
            int found = -1;
            foreach (var item in list.Skip(1))
            {
              if (string.Compare(item, value, StringComparison.OrdinalIgnoreCase) is int index && index >= 0)
              {
                found = index == 0 ? -2 : i;
                break;
              }

              i++;
            }

            if (found == -1)
            {
              list.Add(value);
            }
            else if (found > 0)
            {
              list.Insert(found, value);
            }
          }
        }
      }
    }

    private HitLogRow CreateRow(Dictionary<string, HitLogRow> rowCache, PlayerStats playerStats, IAction action, double currentTime, bool defending = false)
    {
      HitLogRow row = null;

      if (action is DamageRecord damage && !defending && !string.IsNullOrEmpty(damage.Attacker) && !string.IsNullOrEmpty(playerStats.OrigName) && damage.Type != Labels.MISS)
      {
        bool isPet = false;
        if (damage.Attacker.Equals(playerStats.OrigName, StringComparison.OrdinalIgnoreCase) ||
        (isPet = playerStats.OrigName.Equals(PlayerManager.Instance.GetPlayerFromPet(damage.Attacker), StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(damage.AttackerOwner) && damage.AttackerOwner.Equals(playerStats.OrigName, StringComparison.OrdinalIgnoreCase))))
        {
          row = new HitLogRow() { Actor = damage.Attacker, Acted = damage.Defender, IsPet = isPet };
        }
      }
      else if (action is DamageRecord tanking && defending && !string.IsNullOrEmpty(tanking.Defender) && !string.IsNullOrEmpty(playerStats.OrigName) && tanking.Type != Labels.MISS)
      {
        if (tanking.Defender.Equals(playerStats.OrigName, StringComparison.OrdinalIgnoreCase))
        {
          row = new HitLogRow() { Actor = tanking.Defender, Acted = tanking.Attacker, IsPet = false };
        }
      }
      else if (action is HealRecord heal && !string.IsNullOrEmpty(heal.Healer) && !string.IsNullOrEmpty(playerStats.OrigName))
      {
        if (heal.Healer.Equals(playerStats.OrigName, StringComparison.OrdinalIgnoreCase))
        {
          row = new HitLogRow() { Actor = heal.Healer, Acted = heal.Healed, IsPet = false };
        }
      }

      if (row != null && action is HitRecord hit)
      {
        row.Type = hit.Type;
        row.SubType = hit.SubType;
        row.Time = currentTime;

        if (CurrentGroupSpellCastsFilter)
        {
          var rowKey = GetRowKey(row);
          if (rowCache.TryGetValue(rowKey, out HitLogRow previous))
          {
            if (row.Acted != previous.Acted && previous.Acted != "Multiple")
            {
              previous.Acted = "Multiple";
            }

            row = previous;
          }
          else
          {
            rowCache[rowKey] = row;
          }
        }

        row.IsGroupingEnabled = CurrentGroupSpellCastsFilter;
        row.Total += hit.Total;
        row.OverTotal += hit.OverTotal;
        row.CritCount += (uint)(LineModifiersParser.IsCrit(hit.ModifiersMask) ? 1 : 0);
        row.LuckyCount += (uint)(LineModifiersParser.IsLucky(hit.ModifiersMask) ? 1 : 0);
        row.TwincastCount += (uint)(LineModifiersParser.IsTwincast(hit.ModifiersMask) ? 1 : 0);
        row.Count++;
      }

      return row;
    }

    private static string GetRowKey(HitLogRow row)
    {
      return string.Format(CultureInfo.CurrentCulture, "{0}-{1}-{2}", row.Actor, row.SubType, row.Time);
    }

    private void LoadingRow(object sender, DataGridRowEventArgs e)
    {
      // set header count
      e.Row.Header = (e.Row.GetIndex() + 1).ToString(CultureInfo.CurrentCulture);
    }

    private void OptionsChanged(object sender, EventArgs e)
    {
      if (dataGrid != null && Records.Count > 0)
      {
        CurrentActedFilter = actedList.SelectedIndex == 0 ? null : actedList.SelectedItem as string;
        CurrentActionFilter = actionList.SelectedIndex == 0 ? null : actionList.SelectedItem as string;
        CurrentTypeFilter = typeList.SelectedIndex == 0 ? null : typeList.SelectedItem as string;
        CurrentShowPetsFilter = showPets.IsChecked.Value;

        var refresh = CurrentGroupSpellCastsFilter == groupHits.IsChecked.Value;
        CurrentGroupSpellCastsFilter = groupHits.IsChecked.Value;

        if (refresh)
        {
          (dataGrid.ItemsSource as ICollectionView)?.Refresh();
        }
        else
        {
          Display();
        }
      }
    }
  }
}
