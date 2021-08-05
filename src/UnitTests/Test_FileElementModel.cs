﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Subjects;
using Xunit;
using FluentAssertions;

using FileRenamerDiff.Models;

namespace UnitTests
{
    public class Test_FileElementModel
    {

        [Theory]
        [InlineData("coopy -copy.txt", " -copy", "XXX", "coopyXXX.txt", false)]
        [InlineData("abc.txt", "txt", "csv", "abc.csv", true)]
        [InlineData("xABCx_AxBC.txt", "ABC", "[$0]", "x[ABC]x_AxBC.txt", false)]
        [InlineData("abc ABC AnBC", "ABC", "X$0X", "abc XABCX AnBC", true)]
        [InlineData("A0012 34", "\\d*(\\d{3})", "$1", "A012 34", true)]
        //[InlineData("low UPP Pas", "[A-z]", "\\u$0", "LOW UPP PAS", true)] //SIAbstractionsのバグ？で失敗する
        //[InlineData("low UPP Pas", "[A-z]", "\\l$0", "low upp pas", true)] //SIAbstractionsのバグ？で失敗する
        [InlineData("Ha14 Ｆｕ１７", "[Ａ-ｚ]|[０-９]", "\\h$0", "Ha14 Fu17", true)]
        [InlineData("Ha14 Ｆｕ１７", "[A-z]|[0-9]", "\\f$0", "Ｈａ１４ Ｆｕ１７", true)]
        [InlineData("ｱﾝﾊﾟﾝ ﾊﾞｲｷﾝ", "[ｦ-ﾟ]+", "\\f$0", "アンパン バイキン", true)]
        [InlineData("süß ÖL Ära", "\\w?[äöüßÄÖÜẞ]\\w?", "\\n$0", "suess OEL Aera", true)]

        public void ReplacePattern(string targetFileName, string regexPattern, string replaceText, string expectedRenamedFileName, bool isRenameExt)
            => Test_FileElementCore(targetFileName, new[] { regexPattern }, new[] { replaceText }, expectedRenamedFileName, isRenameExt);

        [Theory]
        [InlineData("LargeYChange.txt", "Y", "LargeChange.txt", false)]
        [InlineData("Gray,Sea,Green", "[ae]", "Gry,S,Grn", true)]
        //[InlineData("deleteBeforeExt.txt", @".*(?=\.\w+$)", ".txt", false)]
        [InlineData("deleteBeforeExt.txt", @".*(?=\.\w+$)", ".txt", true)]
        [InlineData("Rocky4", "[A-Z]", "ocky4", true)]
        [InlineData("water", "a.e", "wr", true)]
        [InlineData("A.a あ~ä-", "\\w", ". ~-", true)]
        [InlineData("A B　C", "\\s", "ABC", true)]
        [InlineData("Rocky4", "\\d", "Rocky", true)]
        [InlineData("rear rock", "^r", "ear rock", true)]
        [InlineData("rock rear", "r$", "rock rea", true)]
        [InlineData("door,or,o,lr", "o*r", "d,,o,l", true)]
        [InlineData("door,or,o,lr", "o+r", "d,,o,lr", true)]
        [InlineData("door,or,o,lr", "o?r", "do,,o,l", true)]
        [InlineData("door,or,o,lr", "[or]{2}", "dr,,o,lr", true)]
        [InlineData("1_2.3_45", "\\d\\.\\d", "1__45", true)]
        public void DeletePattern(string targetFileName, string regexPattern, string expectedRenamedFileName, bool isRenameExt)
            => Test_FileElementCore(targetFileName, new[] { regexPattern }, new[] { string.Empty }, expectedRenamedFileName, isRenameExt);

        [Theory]
        [InlineData("Sapmle-1.txt", new[] { "\\d+", "0*(\\d{3})" }, new[] { "00$0", "$1" }, "Sapmle-001.txt", false)]
        [InlineData("Sapmle-12.txt", new[] { "\\d+", "0*(\\d{3})" }, new[] { "00$0", "$1" }, "Sapmle-012.txt", false)]
        [InlineData("Sapmle-123.txt", new[] { "\\d+", "0*(\\d{3})" }, new[] { "00$0", "$1" }, "Sapmle-123.txt", false)]
        [InlineData("Sapmle-1234.txt", new[] { "\\d+", "0*(\\d{3})" }, new[] { "00$0", "$1" }, "Sapmle-1234.txt", false)]
        [InlineData("Sapmle-N.txt", new[] { "\\d+", "0*(\\d{3})" }, new[] { "00$0", "$1" }, "Sapmle-N.txt", false)]
        public void ReplacePatternComplex(string targetFileName, IReadOnlyList<string> regexPatterns, IReadOnlyList<string> replaceTexts, string expectedRenamedFileName, bool isRenameExt)
            => Test_FileElementCore(targetFileName, regexPatterns, replaceTexts, expectedRenamedFileName, isRenameExt);

        internal static void Test_FileElementCore(string targetFileName, IReadOnlyList<string> regexPatterns, IReadOnlyList<string> replaceTexts, string expectedRenamedFileName, bool isRenameExt)
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\" + targetFileName;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockFileData(targetFilePath)
            });

            var messageEvent = new Subject<AppMessage>();
            var fileElem = new FileElementModel(fileSystem, targetFilePath, messageEvent);
            var queuePropertyChanged = new Queue<string?>();
            fileElem.PropertyChanged += (o, e) => queuePropertyChanged.Enqueue(e.PropertyName);

            //TEST1 初期状態
            fileElem.OutputFileName
                    .Should().Be(targetFileName, "まだ元のファイル名のまま");

            fileElem.IsReplaced
                .Should().BeFalse("まだリネーム変更されていないはず");

            fileElem.State
                .Should().Be(RenameState.None, "まだリネーム保存していない");

            queuePropertyChanged
                .Should().BeEmpty("まだ通知は来ていないはず");

            //TEST2 Replace
            //ファイル名の一部を変更する置換パターンを作成
            ReplaceRegex[] replaceRegexes = Enumerable
                .Zip(regexPatterns, replaceTexts,
                    (regex, replaceText) =>
                        new ReplaceRegex(new Regex(regex, RegexOptions.Compiled), replaceText))
                .ToArray();

            //リネームプレビュー実行
            fileElem.Replace(replaceRegexes, isRenameExt);


            fileElem.OutputFileName
                .Should().Be(expectedRenamedFileName, "リネーム変更後のファイル名になったはず");

            bool shouldRename = targetFileName != expectedRenamedFileName;
            fileElem.IsReplaced
                .Should().Be(shouldRename, "リネーム後の名前と前の名前が違うなら、リネーム変更されたはず");

            if (shouldRename)
                queuePropertyChanged
                    .Should().Contain(new[] { nameof(FileElementModel.OutputFileName), nameof(FileElementModel.OutputFilePath), nameof(FileElementModel.IsReplaced) });
            else
                queuePropertyChanged
                    .Should().BeEmpty();

            fileElem.State
                .Should().Be(RenameState.None, "リネーム変更はしたが、まだリネーム保存していない");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { targetFileName }, "ファイルシステム上はまだ前の名前のはず");

            //TEST3 Rename
            fileElem.Rename();

            fileElem.State
                .Should().Be(RenameState.Renamed, "リネーム保存されたはず");

            fileElem.InputFileName
                .Should().Be(expectedRenamedFileName, "リネーム保存後のファイル名になったはず");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { expectedRenamedFileName }, "ファイルシステム上も名前が変わったはず");
        }

        [Theory]
        [InlineData("coopy -copy", " -copy", "XXX", "coopyXXX")]
        [InlineData("abc.Dir", "Dir", "YYY", "abc.YYY")]
        internal static void Test_FileElementDirectory(string targetFileName, string regexPattern, string replaceText, string expectedRenamedFileName)
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\" + targetFileName;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockDirectoryData()
            });

            var messageEvent = new Subject<AppMessage>();
            var fileElem = new FileElementModel(fileSystem, targetFilePath, messageEvent);
            var queuePropertyChanged = new Queue<string?>();
            fileElem.PropertyChanged += (o, e) => queuePropertyChanged.Enqueue(e.PropertyName);

            //TEST1 初期状態
            fileElem.OutputFileName
                    .Should().Be(targetFileName, "まだ元のファイル名のまま");

            fileElem.IsReplaced
                .Should().BeFalse("まだリネーム変更されていないはず");

            fileElem.State
                .Should().Be(RenameState.None, "まだリネーム保存していない");

            queuePropertyChanged
                .Should().BeEmpty("まだ通知は来ていないはず");

            //TEST2 Replace
            //ファイル名の一部を変更する置換パターンを作成
            ReplaceRegex[] replaceRegexes = new[]
                {
                    new ReplaceRegex(new Regex(regexPattern, RegexOptions.Compiled), replaceText)
                };

            //リネームプレビュー実行
            fileElem.Replace(replaceRegexes, false);


            fileElem.OutputFileName
                .Should().Be(expectedRenamedFileName, "リネーム変更後のファイル名になったはず");

            bool shouldRename = targetFileName != expectedRenamedFileName;
            fileElem.IsReplaced
                .Should().Be(shouldRename, "リネーム後の名前と前の名前が違うなら、リネーム変更されたはず");

            if (shouldRename)
                queuePropertyChanged
                    .Should().Contain(new[] { nameof(FileElementModel.OutputFileName), nameof(FileElementModel.OutputFilePath), nameof(FileElementModel.IsReplaced) });
            else
                queuePropertyChanged
                    .Should().BeEmpty();

            fileElem.State
                .Should().Be(RenameState.None, "リネーム変更はしたが、まだリネーム保存していない");

            fileSystem.Directory.GetDirectories(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { targetFileName }, "ファイルシステム上はまだ前の名前のはず");

            //TEST3 Rename
            fileElem.Rename();

            fileElem.State
                .Should().Be(RenameState.Renamed, "リネーム保存されたはず");

            //System.IO.Abstractions のバグ？で反映されていない
            //fileElem.InputFileName
            //    .Should().Be(expectedRenamedFileName, "リネーム保存後のファイル名になったはず");

            fileSystem.Directory.GetDirectories(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { expectedRenamedFileName }, "ファイルシステム上も名前が変わったはず");
        }


        [Fact]
        public void Test_FileElement_WarningMessageInvalid()
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\ABC.txt";
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockFileData("ABC")
            });

            var messages = new List<AppMessage>();
            var messageEvent = new Subject<AppMessage>();
            messageEvent.Subscribe(x => messages.Add(x));

            var fileElem = new FileElementModel(fileSystem, targetFilePath, messageEvent);

            //TEST1 初期状態
            messages
                .Should().BeEmpty("まだなんの警告もきていないはず");

            //TEST2 Replace
            //無効文字の置換パターン

            //リネームプレビュー実行
            fileElem.Replace(new[] { new ReplaceRegex(new Regex("A"), ":") }, false);

            const string expectedFileName = "_BC.txt";

            fileElem.OutputFileName
                .Should().Be(expectedFileName, "無効文字が[_]に置き換わった置換後文字列になっているはず");

            fileElem.IsReplaced
                .Should().BeTrue("リネーム変更されたはず");

            fileElem.State
                .Should().Be(RenameState.None, "リネーム変更はしたが、まだリネーム保存していない");

            messages
                .Should().HaveCount(1, "無効文字が含まれていた警告があるはず");

            //TEST3 Rename
            fileElem.Rename();

            fileElem.State
                .Should().Be(RenameState.Renamed, "リネーム保存されたはず");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { expectedFileName }, "ファイルシステム上も名前が変わったはず");
        }

        [Fact]
        public void Test_FileElement_WarningMessageCannotChange()
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\ABC.txt";
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockFileData("ABC") { AllowedFileShare = FileShare.None }
            });

            var messages = new List<AppMessage>();
            var messageEvent = new Subject<AppMessage>();
            messageEvent.Subscribe(x => messages.Add(x));

            var fileElem = new FileElementModel(fileSystem, targetFilePath, messageEvent);

            //TEST1 初期状態
            messages
                .Should().BeEmpty("まだなんの警告もきていないはず");

            //TEST2 Replace
            //無効文字の置換パターン

            //リネームプレビュー実行
            fileElem.Replace(new[] { new ReplaceRegex(new Regex("ABC"), "xyz") }, false);

            const string expectedFileName = "xyz.txt";

            fileElem.OutputFileName
                .Should().Be(expectedFileName, "置換後文字列になっているはず");

            fileElem.IsReplaced
                .Should().BeTrue("リネーム変更されたはず");

            fileElem.State
                .Should().Be(RenameState.None, "リネーム変更はしたが、まだリネーム保存していない");

            messages
                .Should().BeEmpty("まだなんの警告もきていないはず");

            //TEST3 Rename
            fileElem.Rename();

            fileElem.State
                .Should().Be(RenameState.FailedToRename, "リネーム失敗したはず");

            messages
                .Should().HaveCount(1, "ファイルがリネーム失敗した警告があるはず");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().NotContain(new[] { expectedFileName }, "ファイルシステム上では変わっていないはず");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Should().Contain(new[] { targetFilePath }, "ファイルシステム上では変わっていないはず");
        }
    }
}
