using System;
using Xunit;
using FileRenamerDiff.Models;
using System.Collections.Generic;
using FluentAssertions;
using System.Text.RegularExpressions;
using System.IO.Abstractions.TestingHelpers;
using System.IO;
using System.Linq;

namespace UnitTests
{
    public class UnitTest1
    {
        [Fact]
        public void Test_ValueHolder()
        {
            var queuePropertyChanged = new Queue<string?>();
            var holder = ValueHolderFactory.Create(string.Empty);

            holder.PropertyChanged += (o, e) => queuePropertyChanged.Enqueue(e.PropertyName);

            holder.Value
                .Should().BeEmpty("�����l�͋�̂͂�");

            queuePropertyChanged
                .Should().BeEmpty("�܂��ʒm�͗��Ă��Ȃ��͂�");

            const string newValue = "NEW_VALUE";
            holder.Value = newValue;

            holder.Value
                .Should().Be(newValue, "�V�����l�ɕς���Ă���͂�");

            queuePropertyChanged.Dequeue()
                    .Should().Be(nameof(ValueHolder<string>.Value), "Value�v���p�e�B�̕ύX�ʒm���������͂�");
        }

        [Theory]
        [InlineData("coopy -copy.txt", " -copy", "XXX", "coopyXXX.txt", false)]
        [InlineData("abc.txt", "txt", "csv", "abc.csv", true)]
        [InlineData("LargeYChange.txt", "Y", "", "LargeChange.txt", false)]
        [InlineData("xABCx_AxBC.txt", "ABC", "[$0]", "x[ABC]x_AxBC.txt", false)]
        //[InlineData("deleteBeforeExt.txt", @".*(?=\.\w+$)", "", ".txt", false)]
        [InlineData("deleteBeforeExt.txt", @".*(?=\.\w+$)", "", ".txt", true)]
        public void Test_FileElement(string targetFileName, string regexPattern, string replaceText, string expectedRenamedFileName, bool isRenameExt)
        {
            string targetFilePath = @"D:\FileRenamerDiff_Test\" + targetFileName;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                [targetFilePath] = new MockFileData(targetFilePath)
            });

            var fileElem = new FileElementModel(fileSystem, targetFilePath);
            var queuePropertyChanged = new Queue<string?>();
            fileElem.PropertyChanged += (o, e) => queuePropertyChanged.Enqueue(e.PropertyName);

            //TEST1 �������
            fileElem.OutputFileName
                    .Should().Be(targetFileName, "�܂����̃t�@�C�����̂܂�");

            fileElem.IsReplaced
                .Should().BeFalse("�܂����l�[���ύX����Ă��Ȃ��͂�");

            fileElem.State
                .Should().Be(RenameState.None, "�܂����l�[���ۑ����Ă��Ȃ�");

            queuePropertyChanged
                .Should().BeEmpty("�܂��ʒm�͗��Ă��Ȃ��͂�");

            //TEST2 Replace
            //�t�@�C�����̈ꕔ��XXX�ɕύX����u���p�^�[�����쐬
            var regex = new Regex(regexPattern, RegexOptions.Compiled);
            var rpRegex = new ReplaceRegex(regex, replaceText);

            //���l�[���v���r���[���s
            fileElem.Replace(new[] { rpRegex }, isRenameExt);


            fileElem.OutputFileName
                .Should().Be(expectedRenamedFileName, "���l�[���ύX��̃t�@�C�����ɂȂ����͂�");

            fileElem.IsReplaced
                .Should().BeTrue("���l�[���ύX���ꂽ�͂�");

            queuePropertyChanged
                .Should().Contain(new[] { nameof(FileElementModel.OutputFileName), nameof(FileElementModel.OutputFilePath), nameof(FileElementModel.IsReplaced) });

            fileElem.State
                .Should().Be(RenameState.None, "���l�[���ύX�͂������A�܂����l�[���ۑ����Ă��Ȃ�");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { targetFileName }, "�t�@�C���V�X�e����͂܂��O�̖��O�̂͂�");

            //TEST3 Rename
            fileElem.Rename();

            fileElem.State
                .Should().Be(RenameState.Renamed, "���l�[���ۑ����ꂽ�͂�");

            fileElem.InputFileName
                .Should().Be(expectedRenamedFileName, "���l�[���ۑ���̃t�@�C�����ɂȂ����͂�");

            fileSystem.Directory.GetFiles(Path.GetDirectoryName(targetFilePath))
                .Select(p => Path.GetFileName(p))
                .Should().BeEquivalentTo(new[] { expectedRenamedFileName }, "�t�@�C���V�X�e��������O���ς�����͂�");
        }

        [Fact]
        public void Test_AppMessage()
        {
            var queuePropertyChanged = new Queue<string?>();

            var messages = new AppMessage[]
            {
                new (AppMessageLevel.Info, "HEADTEXT", "A1"),
                new (AppMessageLevel.Info, "HEADTEXT", "A2"),
                new (AppMessageLevel.Info, "HEADTEXT", "A3"),
                new (AppMessageLevel.Info, "OTHER_HEAD", "B1"),
                new (AppMessageLevel.Info, "OTHER_HEAD", "B2"),
                new (AppMessageLevel.Info, "HEADTEXT", "C1"),
                new (AppMessageLevel.Info, "HEADTEXT", "C2"),
                new (AppMessageLevel.Alert, "SINGLE", "D1"),
                new (AppMessageLevel.Error, "MIX_LEVEL", "E1"),
                new (AppMessageLevel.Alert, "MIX_LEVEL", "E2"),
                new (AppMessageLevel.Info, "MIX_LEVEL", "E3"),
            };

            var sumMessages = new Queue<AppMessage>(AppMessageExt.SumSameHead(messages));

            var sum1 = sumMessages.Dequeue();
            sum1.MessageLevel
                .Should().Be(AppMessageLevel.Info);

            sum1.MessageHead
                .Should().Be("HEADTEXT");

            sum1.MessageBody
                .Should().Be($"A1{Environment.NewLine}A2{Environment.NewLine}A3");


            var sum2 = sumMessages.Dequeue();
            sum2.MessageLevel
                .Should().Be(AppMessageLevel.Info);

            sum2.MessageHead
                .Should().Be("OTHER_HEAD");

            sum2.MessageBody
                .Should().Be($"B1{Environment.NewLine}B2");

            var sum3 = sumMessages.Dequeue();
            sum3.MessageLevel
                .Should().Be(AppMessageLevel.Info);

            sum3.MessageHead
                .Should().Be("HEADTEXT");

            sum3.MessageBody
                .Should().Be($"C1{Environment.NewLine}C2");


            var sum4 = sumMessages.Dequeue();
            sum4.MessageLevel
                .Should().Be(AppMessageLevel.Alert);

            sum4.MessageHead
                .Should().Be("SINGLE");

            sum4.MessageBody
                .Should().Be("D1");

            var sum5 = sumMessages.Dequeue();
            sum5.MessageLevel
                .Should().Be(AppMessageLevel.Error);

            sum5.MessageHead
                .Should().Be("MIX_LEVEL");

            sum5.MessageBody
                .Should().Be($"E1{Environment.NewLine}E2{Environment.NewLine}E3");
        }
    }
}
