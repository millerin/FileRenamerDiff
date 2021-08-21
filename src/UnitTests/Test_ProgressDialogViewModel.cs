﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using FileRenamerDiff.Models;
using FileRenamerDiff.ViewModels;

using FluentAssertions;

using Moq;

using Reactive.Bindings;

using Xunit;

namespace UnitTests
{
    public class Test_ProgressDialogViewModel
    {
        [WpfFact]
        public async Task Test_ProgressDialogViewModel_ProgressInfo()
        {
            var mock = new Mock<IMainModel>();
            mock.SetupGet(x => x.UIScheduler)
                .Returns(new SynchronizationContextScheduler(SynchronizationContext.Current!));

            var subjectProgress = new Subject<ProgressInfo>();

            mock
                .SetupGet(x => x.CurrentProgressInfo)
                .Returns(subjectProgress.ToReadOnlyReactivePropertySlim());

            var vm = new ProgressDialogViewModel(mock.Object);

            Enumerable.Range(0, 3)
                .Select(x => new ProgressInfo(x, $"progress-{x:00}"))
                .ForEach(x => subjectProgress.OnNext(x));

            await Task.Delay(1000);

            vm.CurrentProgressInfo.Value!.Count
                .Should().Be(2);
            vm.CurrentProgressInfo.Value!.Message
                .Should().Contain("progress-02");
            vm.CurrentProgressInfo.Value!.Message
                .Should().NotContainAny("00", "01");
        }

        [WpfFact]
        public async Task Test_ProgressDialogViewModel_Cancel()
        {
            var mock = new Mock<IMainModel>();

            mock.SetupGet(x => x.UIScheduler)
                .Returns(new SynchronizationContextScheduler(SynchronizationContext.Current!));

            var subjectProgress = new Subject<ProgressInfo>();
            mock
                .SetupGet(x => x.CurrentProgressInfo)
                .Returns(subjectProgress.ToReadOnlyReactivePropertySlim());

            var cancelToken = new CancellationTokenSource();
            mock
                .SetupGet(x => x.CancelWork)
                .Returns(cancelToken);

            var vm = new ProgressDialogViewModel(mock.Object);

            cancelToken.IsCancellationRequested
                .Should().BeFalse();

            await vm.CancelCommand.ExecuteAsync();
            cancelToken.IsCancellationRequested
                .Should().BeTrue();

            await vm.CancelCommand.ExecuteAsync();
            cancelToken.IsCancellationRequested
                .Should().BeTrue();
        }
    }
}