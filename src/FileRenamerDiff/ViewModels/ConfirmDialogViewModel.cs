﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Resources;
using System.Globalization;
using System.Windows.Data;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

using Livet;
using Livet.Commands;
using Livet.Messaging;
using Livet.Messaging.IO;
using Livet.EventListeners;
using Livet.Messaging.Windows;

using Reactive.Bindings;
using System.Reactive;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;
using Anotar.Serilog;

using FileRenamerDiff.Models;
using FileRenamerDiff.Properties;

namespace FileRenamerDiff.ViewModels
{
    public class ConfirmDialogViewModel : ViewModel
    {
        /// <summary>
        /// ダイアログ結果（初期状態はNull）
        /// </summary>
        public ReactivePropertySlim<bool?> IsOkResult { get; } = new ReactivePropertySlim<bool?>(null);

        public ReactiveCommand OkCommand { get; } = new();
        public ReactiveCommand CancelCommand { get; } = new();

        public ConfirmDialogViewModel()
        {
            OkCommand.Subscribe(() =>
                IsOkResult.Value = true);

            CancelCommand.Subscribe(() =>
                IsOkResult.Value = false);
        }
    }
}
