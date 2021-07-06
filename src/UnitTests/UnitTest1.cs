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
    }
}
