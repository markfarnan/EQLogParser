﻿using System.Windows;
using System.Windows.Controls;

namespace EQLogParser
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
      if (MainWindow.WindowState == WindowState.Maximized)
      {
        MainWindow.WindowState = WindowState.Normal;
      }
      else
      {
        MainWindow.WindowState = WindowState.Maximized;
      }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
      MainWindow.WindowState = WindowState.Minimized;
    }

    private void Close_MouseClick(object sender, RoutedEventArgs e)
    {
      MainWindow.Close();
    }

    private void CloseOverlay_MouseClick(object sender, RoutedEventArgs e)
    {
      (Current.MainWindow as MainWindow).ResetOverlay();
    }

    private void CloseDialog_MouseClick(object sender, RoutedEventArgs e)
    {
      Window.GetWindow(sender as Button).Close();
    }
  }
}
