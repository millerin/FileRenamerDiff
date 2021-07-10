﻿using System;
using Xunit;
using FileRenamerDiff.Models;
using System.Collections.Generic;
using FluentAssertions;
using System.Text.RegularExpressions;
using System.IO.Abstractions.TestingHelpers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        [InlineData("Ha14 Ｆｕ１７", ".", "\\h$0", "Ha14 Fu17", true)]
        [InlineData("Ha14 Ｆｕ１７", ".", "\\f$0", "Ｈａ１４ Ｆｕ１７", true)]
        [InlineData("ｱﾝﾊﾟﾝ ﾊﾞｲｷﾝ", "[ｦ-ﾟ]+", "\\f$0", "アンパン バイキン", true)]
        [InlineData("süß ÖL Ära", "\\w?[äöüßÄÖÜẞ]\\w?", "\\n$0", "suess OEL Aera", true)]

        public void ReplacePattern(string targetFileName, string regexPattern, string replaceText, string expectedRenamedFileName, bool isRenameExt)
            => Test_FileElementCore(targetFileName, regexPattern, replaceText, expectedRenamedFileName, isRenameExt);

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
            => Test_FileElementCore(targetFileName, regexPattern, "", expectedRenamedFileName, isRenameExt);

        internal void Test_FileElementCore(string targetFileName, string regexPattern, string replaceText, string expectedRenamedFileName, bool isRenameExt)
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\" + targetFileName;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockFileData(targetFilePath)
            });

            var fileElem = new FileElementModel(fileSystem, targetFilePath);
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
            //ファイル名の一部をXXXに変更する置換パターンを作成
            var regex = new Regex(regexPattern, RegexOptions.Compiled);
            var rpRegex = new ReplaceRegex(regex, replaceText);

            //リネームプレビュー実行
            fileElem.Replace(new[] { rpRegex }, isRenameExt);


            fileElem.OutputFileName
                .Should().Be(expectedRenamedFileName, "リネーム変更後のファイル名になったはず");

            fileElem.IsReplaced
                .Should().BeTrue("リネーム変更されたはず");

            queuePropertyChanged
                .Should().Contain(new[] { nameof(FileElementModel.OutputFileName), nameof(FileElementModel.OutputFilePath), nameof(FileElementModel.IsReplaced) });

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
    }
}
