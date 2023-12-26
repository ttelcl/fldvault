﻿/*
 * (c) 2019   / TTELCL
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZvaultKeyServer.WpfUtilities;

/// <summary>
/// Generic base class for ViewModels
/// </summary>
public abstract class ViewModelBase: INotifyPropertyChanged
{
  /// <summary>
  /// Create a new ViewModelBase
  /// </summary>
  protected ViewModelBase()
  {
  }

  /// <summary>
  /// Implements the INotifyPropertyChanged contract
  /// </summary>
  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>
  /// Raises the PropertyChanged event. Consider using SetProperty to
  /// call this indirectly
  /// </summary>
  /// <param name="propertyName">
  /// The name of the property, by default the name of the caller
  /// (via CallerMemberName magic)
  /// </param>
  protected void RaisePropertyChanged(
    [CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

  /// <summary>
  /// Changes the property value stored in the provided storage location,
  /// raising PropertyChanged if the value actually changed.
  /// The values are compared using EqualityComparer{T}.Default.
  /// Returns true if the value changed, false if the value didn't change.
  /// </summary>
  protected bool SetValueProperty<T>(
    ref T storage, T value, [CallerMemberName] string propertyName = "")
  {
    if(EqualityComparer<T>.Default.Equals(storage, value))
    {
      return false;
    }
    else
    {
      storage = value;
      RaisePropertyChanged(propertyName);
      return true;
    }
  }

  /// <summary>
  /// Changes the property value stored in the provided storage location,
  /// raising PropertyChanged if the value actually changed.
  /// The values are compared using Object.ReferenceEquals.
  /// Returns true if the value changed, false if the value didn't change.
  /// </summary>
  protected bool SetInstanceProperty<T>(
    ref T storage, T value, [CallerMemberName] string propertyName = "") where T : class
  {
    if(ReferenceEquals(storage, value))
    {
      return false;
    }
    else
    {
      storage = value;
      RaisePropertyChanged(propertyName);
      return true;
    }
  }

  /// <summary>
  /// Changes the property value stored in the provided storage location,
  /// raising PropertyChanged unconditionally (even if the new and old values
  /// are equal).
  /// </summary>
  protected void ForceInstanceProperty<T>(
    ref T storage, T value, [CallerMemberName] string propertyName = "") where T : class
  {
    storage = value;
    RaisePropertyChanged(propertyName);
  }

  /// <summary>
  /// Changes the property value stored in the provided storage location,
  /// raising PropertyChanged if the value actually changed.
  /// The values are compared using Object.ReferenceEquals.
  /// Returns true if the value changed, false if the value didn't change.
  /// </summary>
  protected bool SetNullableInstanceProperty<T>(
    ref T? storage, T? value, [CallerMemberName] string propertyName = "") where T : class?
  {
    if(ReferenceEquals(storage, value))
    {
      return false;
    }
    else
    {
      storage = value;
      RaisePropertyChanged(propertyName);
      return true;
    }
  }

  /// <summary>
  /// Raise the PropertChanged event for a value type if the old value and new value
  /// are different. 
  /// </summary>
  protected bool CheckValueProperty<T>(
    T oldValue,
    T newValue,
    [CallerMemberName] string propertyName = "")
  {
    if(EqualityComparer<T>.Default.Equals(oldValue, newValue))
    {
      return false;
    }
    else
    {
      RaisePropertyChanged(propertyName);
      return true;
    }
  }

  /// <summary>
  /// Raise the PropertChanged event for a reference type if the old value and new value
  /// are different instances
  /// </summary>
  protected bool CheckInstanceProperty<T>(
    T oldValue,
    T newValue,
    [CallerMemberName] string propertyName = "") where T : class
  {
    if(ReferenceEquals(oldValue, newValue))
    {
      return false;
    }
    else
    {
      RaisePropertyChanged(propertyName);
      return true;
    }
  }

}