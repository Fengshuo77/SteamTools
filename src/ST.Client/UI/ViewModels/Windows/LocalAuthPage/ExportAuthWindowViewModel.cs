using ReactiveUI;
using System.Application.Models;
using System.Application.Services;
using System.Application.UI.Resx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Properties;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Application.FilePicker2;
using _ThisAssembly = System.Properties.ThisAssembly;

// ReSharper disable once CheckNamespace
namespace System.Application.UI.ViewModels
{
    public partial class ExportAuthWindowViewModel : WindowViewModel
    {
        public static string DisplayName => AppResources.LocalAuth_ExportAuth;

        public override string Title
        {
            get
            {
                var title = GetTitleByDisplayName(DisplayName);
                var mark = SelectAuthenticatorMark;
                if (mark != default)
                {
                    title = $"{title}({mark})";
                }
                return title;
            }
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// 选中Id，当此值有效时，仅导出此值对应的令牌
        /// </summary>
        public ushort SelectId { get; set; }

        /// <summary>
        /// 当前选中令牌
        /// </summary>
        MyAuthenticator? SelectAuthenticator
        {
            get
            {
                if (SelectId != default)
                {
                    var vm = AuthService.Current.Authenticators.Items.FirstOrDefault(x => x.Id == SelectId);
                    return vm;
                }
                return null;
            }
        }

        /// <summary>
        /// 当前选中令牌唯一显示名称
        /// </summary>
        string? SelectAuthenticatorMark
        {
            get
            {
                var authenticator = SelectAuthenticator;
                if (authenticator != default)
                {
                    return (string.IsNullOrEmpty(authenticator.Name) ? authenticator.Id.ToString() : authenticator.Name);
                }
                return null;
            }
        }

        public ExportAuthWindowViewModel()
        {
        }

        private bool _IsPasswordEncrypt;

        public bool IsPasswordEncrypt
        {
            get => _IsPasswordEncrypt;
            set => this.RaiseAndSetIfChanged(ref _IsPasswordEncrypt, value);
        }

        private string? _Password;

        public string? Password
        {
            get => _Password;
            set => this.RaiseAndSetIfChanged(ref _Password, value);
        }

        private string? _VerifyPassword;

        public string? VerifyPassword
        {
            get => _VerifyPassword;
            set => this.RaiseAndSetIfChanged(ref _VerifyPassword, value);
        }

        private bool _IsOnlyCurrentComputerEncrypt;

        public bool IsOnlyCurrentComputerEncrypt
        {
            get => _IsOnlyCurrentComputerEncrypt;
            set => this.RaiseAndSetIfChanged(ref _IsOnlyCurrentComputerEncrypt, value);
        }

        SaveFileResult? _ExportFile;

        public SaveFileResult? ExportFile
        {
            get => _ExportFile;
            set
            {
                if (_ExportFile != null)
                {
                    _ExportFile.Dispose();
                }
                _ExportFile = value;
            }
        }

        private Stream? _QRCode;

        /// <summary>
        /// 当前导出的二维码图像数据流
        /// </summary>
        public Stream? QRCode
        {
            get => _QRCode;
            set
            {
                var oldValue = _QRCode;
                if (this.RaiseAndSetIfChangedReturnIsNotChange(ref _QRCode, value)) return;
                oldValue?.Dispose();
            }
        }

        private bool _IsExportQRCode;

        /// <summary>
        /// 是否启用导出为二维码
        /// </summary>
        public bool IsExportQRCode
        {
            get => _IsExportQRCode;
            set => this.RaiseAndSetIfChanged(ref _IsExportQRCode, value);
        }

        private bool _IsExporting;

        /// <summary>
        /// 是否正在导出中
        /// </summary>
        public bool IsExporting
        {
            get => _IsExporting;
            set => this.RaiseAndSetIfChanged(ref _IsExporting, value);
        }

        async Task ExportAuthCore(Func<Task> func, bool ignorePath = false, bool ignorePassword = false)
        {
            var (success, _) = await AuthService.Current.HasPasswordEncryptionShowPassWordWindow();
            if (!success)
            {
                Close();
                return;
            }

            if (!ignorePath)
            {
                if (IsSupportedSaveFileDialog)
                {
                    FilePickerFileType? fileTypes;
                    if (IApplication.IsDesktopPlatform)
                    {
                        fileTypes = new ValueTuple<string, string[]>[]
                        {
                            ("MsgPack Files", new[] { FileEx.MPO, }),
                            ("Data Files", new[] { FileEx.DAT, }),
                            //("All Files", new[] { "*", }),
                        };
                    }
                    else
                    {
                        fileTypes = null;
                    }
                    ExportFile = await SaveAsync(new()
                    {
                        FileTypes = fileTypes,
                        InitialFileName = DefaultExportAuthFileName,
                        PickerTitle = ThisAssembly.AssemblyTrademark,
                    });
                    if (ExportFile == null) return;
                }
            }

            if (!ignorePassword)
            {
                if (IsPasswordEncrypt)
                {
                    if (string.IsNullOrWhiteSpace(VerifyPassword) && VerifyPassword != Password)
                    {
                        Toast.Show(AppResources.LocalAuth_ProtectionAuth_PasswordErrorTip);
                        return;
                    }
                }
                else
                {
                    VerifyPassword = null;
                }
            }

            await func();
        }

        public async void ExportAuth()
        {
            IsExporting = true;

            if (IsExportQRCode)
            {
                await ExportAuthToQRCodeAsync();
            }
            else
            {
                await ExportAuthToFileAsync();
            }

            IsExporting = false;
        }

        static Func<MyAuthenticator, bool>? GetFilter(ushort selectId)
        {
            if (selectId == default) return null;
            return x => x.Id == selectId;
        }

        Func<MyAuthenticator, bool>? Filter => GetFilter(SelectId);

        static async Task<Stream?> GetQRCodeAsync(Func<MyAuthenticator, bool>? filter)
        {
            var datas = AuthService.Current.GetExportSourceAuthenticators(filter);
            return await AuthService.GetQrCodeStreamAsync(datas);
        }

        /// <summary>
        /// 根据令牌 Id 生成二维码
        /// </summary>
        /// <param name="selectId"></param>
        /// <returns></returns>
        public static Task<Stream?> GetQRCodeAsync(ushort selectId) => GetQRCodeAsync(GetFilter(selectId));

        async Task ExportAuthToFileAsync() => await ExportAuthCore(() =>
        {
            AuthService.Current.ExportAuthenticators(ExportFile?.OpenWrite(), IsOnlyCurrentComputerEncrypt, VerifyPassword, Filter);

            Close();

            Toast.Show(AppResources.ExportedToPath_.Format(ExportFile?.ToString()));

            ExportFile = null;

            return Task.CompletedTask;
        });

        async Task ExportAuthToQRCodeAsync() => await ExportAuthCore(async () => QRCode = await GetQRCodeAsync(Filter), ignorePath: true, ignorePassword: true);

        /// <summary>
        /// 默认导出文件名
        /// </summary>
        public string DefaultExportAuthFileName
        {
            get
            {
                var mark = SelectAuthenticatorMark;
                var markIsNull = mark == default;
                var now = DateTime.Now;
                const string f = $"{_ThisAssembly.AssemblyTrademark}  Authenticator{{0}} {{1}}{{2}}{FileEx.MPO}";
                return string.Format(f,
                    markIsNull ? "s" : default,
                    markIsNull ? default : $"({mark}) ",
                    now.ToString(DateTimeFormat.File));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _QRCode?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
