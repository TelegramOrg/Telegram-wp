﻿using System.Text;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class PasswordRecoveryViewModel : ViewModelBase
    {
        private string _code;

        public string Code
        {
            get { return _code; }
            set
            {
                SetField(ref _code, value, () => Code);
                NotifyOfPropertyChange(() => CanRecoverPassword);
            }
        }

        private bool _hasError;

        public bool HasError
        {
            get { return _hasError; }
            set { SetField(ref _hasError, value, () => HasError); }
        }

        private string _error = " ";

        public string Error
        {
            get { return _error; }
            set { SetField(ref _error, value, () => Error); }
        }

        public string PasswordRecoveryHint { get; set; }

        public string HavingTroubleHint { get; set; }

        private readonly TLPassword _passwordBase;

        public PasswordRecoveryViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _passwordBase = StateService.Password as TLPassword;
            StateService.Password = null;

            if (_passwordBase != null)
            {
                PasswordRecoveryHint = string.Format(AppResources.PasswordRecoveryHint, _passwordBase.EmailUnconfirmedPattern);
                HavingTroubleHint = string.Format(AppResources.HavingTroubleAccessingEmailMessage, _passwordBase.EmailUnconfirmedPattern);
            }
        }

        protected override void OnActivate()
        {
            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }

            base.OnActivate();
        }

        public bool CanRecoverPassword
        {
            get { return !string.IsNullOrEmpty(Code) && Code.Length == Constants.PasswordRecoveryCodeLength; }
        }

        public void RecoverPassword()
        {
            if (_passwordBase == null) return;
            if (IsWorking) return;

            HasError = false;
            Error = " ";

            IsWorking = true;
            MTProtoService.RecoverPasswordAsync(new TLString(Code), 
                auth => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    if (!_passwordBase.IsAuthRecovery)
                    {
                        MTProtoService.GetPasswordAsync(
                            passwordResult => BeginOnUIThread(() =>
                            {
                                MessageBox.Show(AppResources.PasswordDeactivated, AppResources.AppName, MessageBoxButton.OK);

                                StateService.RemoveBackEntry = true;
                                StateService.Password = passwordResult;
                                NavigationService.UriFor<PasswordViewModel>().Navigate();
                            }),
                            error2 =>
                            {
                                Execute.ShowDebugMessage("account.getPassword error " + error2);
                            });
                    }
                    else
                    {
                        _passwordBase.IsAuthRecovery = false;
#if LOG_REGISTRATION
                        TLUtils.WriteLog("auth.recoverPassword result " + auth);
                        TLUtils.WriteLog("TLUtils.IsLogEnabled=false");
#endif

                        TLUtils.IsLogEnabled = false;
                        TLUtils.LogItems.Clear();

                        var result = MessageBox.Show(
                            AppResources.ConfirmPushMessage,
                            AppResources.ConfirmPushTitle,
                            MessageBoxButton.OKCancel);

                        if (result != MessageBoxResult.OK)
                        {
                            StateService.GetNotifySettingsAsync(settings =>
                            {
                                var s = settings ?? new Settings();
                                s.ContactAlert = false;
                                s.ContactMessagePreview = true;
                                s.ContactSound = StateService.Sounds[0];
                                s.GroupAlert = false;
                                s.GroupMessagePreview = true;
                                s.GroupSound = StateService.Sounds[0];

                                s.InAppMessagePreview = true;
                                s.InAppSound = true;
                                s.InAppVibration = true;

                                StateService.SaveNotifySettingsAsync(s);
                            });

                            MTProtoService.UpdateNotifySettingsAsync(
                                new TLInputNotifyUsers(),
                                new TLInputPeerNotifySettings
                                {
                                    EventsMask = new TLInt(1),
                                    MuteUntil = new TLInt(int.MaxValue),
                                    ShowPreviews = new TLBool(true),
                                    Sound = new TLString(StateService.Sounds[0])
                                },
                                r => { });

                            MTProtoService.UpdateNotifySettingsAsync(
                                new TLInputNotifyChats(),
                                new TLInputPeerNotifySettings
                                {
                                    EventsMask = new TLInt(1),
                                    MuteUntil = new TLInt(int.MaxValue),
                                    ShowPreviews = new TLBool(true),
                                    Sound = new TLString(StateService.Sounds[0])
                                },
                                r => { });
                        }
                        else
                        {
                            StateService.GetNotifySettingsAsync(settings =>
                            {
                                var s = settings ?? new Settings();
                                s.ContactAlert = true;
                                s.ContactMessagePreview = true;
                                s.ContactSound = StateService.Sounds[0];
                                s.GroupAlert = true;
                                s.GroupMessagePreview = true;
                                s.GroupSound = StateService.Sounds[0];

                                s.InAppMessagePreview = true;
                                s.InAppSound = true;
                                s.InAppVibration = true;

                                StateService.SaveNotifySettingsAsync(s);
                            });

                            MTProtoService.UpdateNotifySettingsAsync(
                                new TLInputNotifyUsers(),
                                new TLInputPeerNotifySettings
                                {
                                    EventsMask = new TLInt(1),
                                    MuteUntil = new TLInt(0),
                                    ShowPreviews = new TLBool(true),
                                    Sound = new TLString(StateService.Sounds[0])
                                },
                                r => { });

                            MTProtoService.UpdateNotifySettingsAsync(
                                new TLInputNotifyChats(),
                                new TLInputPeerNotifySettings
                                {
                                    EventsMask = new TLInt(1),
                                    MuteUntil = new TLInt(0),
                                    ShowPreviews = new TLBool(true),
                                    Sound = new TLString(StateService.Sounds[0])
                                },
                                r => { });
                        }

                        MTProtoService.SetInitState();
                        StateService.CurrentUserId = auth.User.Index;
                        StateService.ClearNavigationStack = true;
                        StateService.FirstRun = true;
                        SettingsHelper.SetValue(Constants.IsAuthorizedKey, true);
                        NavigationService.UriFor<ShellViewModel>().Navigate();
                    }
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    var messageBuilder = new StringBuilder();
                    //messageBuilder.AppendLine(AppResources.Error);
                    //messageBuilder.AppendLine();
                    messageBuilder.AppendLine("Method: account.recoveryPassword");
                    messageBuilder.AppendLine("Result: " + error);

                    if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                    {
                        HasError = true;
                        Error = AppResources.FloodWaitString;
                        Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                    {
                        HasError = true;
                        Error = AppResources.ServerError;
                        Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                    {
                        if (TLRPCError.TypeEquals(error, ErrorType.CODE_EMPTY))
                        {
                            HasError = true;
                            Error = string.Format("{0} {1}", error.Code, error.Message);
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.CODE_INVALID))
                        {
                            HasError = true;
                            Error = string.Format("{0} {1}", error.Code, error.Message);
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_EMPTY))
                        {
                            HasError = true;
                            Error = string.Format("{0} {1}", error.Code, error.Message);
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_RECOVERY_NA))
                        {
                            HasError = true;
                            Error = AppResources.EmailInvalid;
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.EmailInvalid, AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_RECOVERY_EXPIRED))
                        {
                            HasError = true;
                            Error = string.Format("{0} {1}", error.Code, error.Message);
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.EmailInvalid, AppResources.Error, MessageBoxButton.OK));
                        }
                        else
                        {
                            HasError = true;
                            Error = string.Format("{0} {1}", error.Code, error.Message);
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                    }
                    else
                    {
                        HasError = true;
                        Error = string.Empty;
                        Execute.ShowDebugMessage("account.recoveryPassword error " + error);
                    }
                }));
        }

        public void ForgotPassword()
        {
            MessageBox.Show(AppResources.NoAccessRecoveryEmailMessage, AppResources.Sorry, MessageBoxButton.OK);
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }
}
