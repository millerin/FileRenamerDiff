# HowToUse html�t�@�C�������X�N���v�g
# Markdown����HTML�t�@�C���𐶐�����
cd 'HowToUse'
$nameHeader = 'how_to_use'
# �e���ꂲ�Ƃ̃R�[�h
$langCodes = @('','.de','.ru','.zh','.ja')

# �e���ꂲ�Ƃ�1��HTML�t�@�C�����ł���
foreach($langCode in $langCodes)
{
    $sourcePath = ".\$nameHeader$langCode.md"
    $sourceTime = $(Get-ItemProperty $sourcePath).LastWriteTime
    $targetPath = "..\Resources\$nameHeader$langCode.html"
    if (Test-Path $targetPath)
    {
        $targetTime = $(Get-ItemProperty $targetPath).LastWriteTime
    }
    else
    {
        $targetTime = 0
    }

    echo "Creation $nameHeader$langCode.html: sourceTime is $sourceTime, targetTime is $targetTime"

    # ��������HTML�t�@�C��������Markdown�t�@�C�����X�V�������V�����Ƃ��̂݃R���o�[�g����
    if ( $sourceTime -gt   $targetTime )
    {
        echo "Start Create $nameHeader$langCode.html"
        # Pandoc���g�p����Markdown����HTML�t�@�C���𐶐�����Bcss�Ȃǂ��w�肷��
        & 'C:\Program Files\Pandoc\pandoc' -s ./$nameHeader$langCode.md -o ../Resources/$nameHeader$langCode.html --toc --template=./elegant_bootstrap_menu.html --self-contained -t html5 -c github.css
        echo "Finished Create $nameHeader$langCode.html"
    }
} 
