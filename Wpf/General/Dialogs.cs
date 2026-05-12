using System.Windows.Forms;

namespace MeloongCore.Wpf;

public static class Dialogs {

    /// <summary>
    /// 弹出 “保存” 弹窗，返回用户选择的路径（若取消则返回 <c>null</c>）。
    /// <para/> <paramref name="filter"/> 中的扩展名不加 <c>.</c>。
    /// </summary>
    public static string? SaveFile(string title, string defaultFileName = "", string? defaultDirectory = null, IEnumerable<(string Extension, string Display)>? filter = null) {
        var filters = filter?.ToList();
        using var dialog = new SaveFileDialog {
            AddExtension = true,
            AutoUpgradeEnabled = true,
            Title = title,
            FileName = defaultFileName
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && Directory.Exists(defaultDirectory)) dialog.InitialDirectory = PathUtils.ToShortPath(defaultDirectory!);
        if (filters != null) {
            dialog.Filter = filters.Select(f => $"{f.Display}(*.{f.Extension})|*.{f.Extension}").Join("|");
        }

        Logger.Info($"保存弹窗：{dialog.Title}（FileName={dialog.FileName}, InitialDirectory={dialog.InitialDirectory}, Filter={dialog.Filter}）");
        dialog.ShowDialog();

        var result = dialog.FileName;
        Logger.Info($"保存弹窗返回：{result}");
        if (!result.Contains('\\')) return null;
        // AddExtension 可能失效，需要手动补全（#8214）
        if (filters != null && !string.IsNullOrEmpty(result) && !filters.Any(f => result.EndsWithF("." + f.Extension))) {
            result += "." + filters[dialog.FilterIndex - 1].Extension;
            Logger.Warn($"选择文件的返回无扩展名，将会手动添加，修改后为：{result}");
        }
        return result;
    }

    /// <summary>
    /// 弹出 “选择文件” 弹窗，返回用户选择的文件路径（若取消则返回空数组）。
    /// <para/> <paramref name="filter"/> 中的扩展名不加 <c>.</c>。
    /// </summary>
    public static string[] SelectFile(string title, bool multiselect, string? defaultDirectory = null, IEnumerable<(string[] Extensions, string Display)>? filter = null) {
        var filters = filter?.ToList();
        using var dialog = new OpenFileDialog {
            AddExtension = true,
            AutoUpgradeEnabled = true,
            CheckFileExists = true,
            Multiselect = multiselect,
            Title = title,
            ValidateNames = true
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && Directory.Exists(defaultDirectory)) dialog.InitialDirectory = PathUtils.ToShortPath(defaultDirectory!);
        if (filters != null) {
            dialog.Filter = filters.Select(f => {
                var exts = string.Join(";", f.Extensions.Select(e => $"*.{e}"));
                return $"{f.Display}({exts})|{exts}";
            }).Join("|");
        }

        Logger.Info($"选择文件弹窗：{dialog.Title}（Multiselect={dialog.Multiselect}, InitialDirectory={dialog.InitialDirectory}, Filter={dialog.Filter}）");
        dialog.ShowDialog();

        var results = multiselect ? dialog.FileNames : (string.IsNullOrEmpty(dialog.FileName) ? [] : [dialog.FileName]);
        Logger.Info($"选择文件弹窗返回：{string.Join(",", results)}");
        return results;
    }

    /// <summary>
    /// 弹出选取文件夹对话框，返回用户选择的文件夹路径（以 \ 结尾），取消则返回空字符串。
    /// </summary>
    public static string SelectFolder(string title, string? defaultDirectory = null) {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog {
            ShowNewFolderButton = true,
            Description = title,
            UseDescriptionForTitle = true
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && Directory.Exists(defaultDirectory)) dialog.SelectedPath = defaultDirectory;

        Logger.Info($"选择文件夹弹窗：{dialog.Description}（SelectedPath={dialog.SelectedPath}）");
        dialog.ShowDialog();

        var result = string.IsNullOrEmpty(dialog.SelectedPath) ? "" : (dialog.SelectedPath!.EndsWithF('\\') ? dialog.SelectedPath! : dialog.SelectedPath! + '\\');
        Logger.Info($"选择文件夹弹窗返回：{result}");
        return result;
    }

}
