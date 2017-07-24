﻿using System.ComponentModel;
using Caliburn.Micro;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Helpers;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Media
{
    public class FullMediaViewModel : Conductor<ViewModelBase>.Collection.OneActive
    {
        public bool IsViewerOpen { get { return ImageViewer != null && ImageViewer.IsOpen; } }

        public IInputPeer CurrentItem { get; protected set; }

        public MediaViewModel<IInputPeer> Media { get; protected set; }

        public FilesViewModel<IInputPeer> Files { get; protected set; }

        public LinksViewModel<IInputPeer> Links { get; protected set; }

        public MusicViewModel<IInputPeer> Music { get; protected set; } 

        private readonly IStateService _stateService;

        public ImageViewerViewModel ImageViewer { get; protected set; }

        public AnimatedImageViewerViewModel AnimatedImageViewer { get { return Files.AnimatedImageViewer; } }

        public FullMediaViewModel(
            MusicViewModel<IInputPeer> music,
            FilesViewModel<IInputPeer> files, 
            LinksViewModel<IInputPeer> links,
            MediaViewModel<IInputPeer> media, 
            IStateService stateService,
            INavigationService navigationService)
        {
            //tombstoning
            if (stateService.CurrentInputPeer == null)
            {
                stateService.ClearNavigationStack = true;
                navigationService.UriFor<ShellViewModel>().Navigate();
                return;
            }

            CurrentItem = stateService.CurrentInputPeer;
            stateService.CurrentInputPeer = null;

            ImageViewer = new ImageViewerViewModel(stateService, IoC.Get<IVideoFileManager>());

            Media = media;
            Media.ImageViewer = ImageViewer;
            Files = files;
            Files.PropertyChanged += OnFilesPropertyChanged;
            Links = links;
            Links.PropertyChanged += OnLinksPropertyChanged;
            Music = music;
            Music.PropertyChanged += OnMusicPropertyChanged;
            _stateService = stateService;
        }

        private void OnMusicPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            
        }

        private void OnLinksPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            
        }

        private void OnFilesPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => Files.AnimatedImageViewer))
            {
                NotifyOfPropertyChange(() => AnimatedImageViewer);
            }
        }

        protected override void OnInitialize()
        {
            if (CurrentItem == null) return;

            Media.CurrentItem = CurrentItem;
            Files.CurrentItem = CurrentItem;
            Links.CurrentItem = CurrentItem;
            Music.CurrentItem = CurrentItem;

            NotifyOfPropertyChange(() => CurrentItem);

            Items.Add(Media);
            Items.Add(Files);
            Items.Add(Links);
            Items.Add(Music);
            if (_stateService.MediaTab)
            {
                _stateService.MediaTab = false;
                //Items.Add(Media);
                //Items.Add(ContactDetails);
                ActivateItem(Media);
            }
            else
            {
                //Items.Add(ContactDetails);
                //Items.Add(Media);
                //ActivateItem(ContactDetails);
            }

            base.OnInitialize();
        }

        public void OnBackKeyPressed()
        {
            _stateService.SharedContact = null;
        }

        public void CancelLoading()
        {
            Media.CancelDownloading();
        }

        public void Search()
        {
            if (ActiveItem == Files)
            {
                Files.Search();
            }
            else if (ActiveItem == Links)
            {
                Links.Search();
            }
            else if (ActiveItem == Music)
            {
                Music.Search();
            }
        }

        public void Manage()
        {
            if (ActiveItem == Files)
            {
                Files.Manage();
            }
            else if (ActiveItem == Links)
            {
                Links.Manage();
            }
            else if (ActiveItem == Music)
            {
                Music.Manage();
            }
        }

        public void Forward()
        {
            if (ActiveItem == Files)
            {
                Files.ForwardMessages();
            }
            else if (ActiveItem == Links)
            {
                Links.ForwardMessages();
            }
            else if (ActiveItem == Music)
            {
                Music.ForwardMessages();
            }
        }

        public void Delete()
        {
            if (ActiveItem == Files)
            {
                Files.DeleteMessages();
            }
            else if (ActiveItem == Links)
            {
                Links.DeleteMessages();
            }
            else if (ActiveItem == Music)
            {
                Music.DeleteMessages();
            }
        }

        public void DeleteMessage(TLMessageBase message)
        {
            if (ActiveItem == Files)
            {
                Files.DeleteMessage(message);
            }
            else if (ActiveItem == Links)
            {
                Links.DeleteMessage(message);
            }
            else if (ActiveItem == Music)
            {
                Music.DeleteMessage(message);
            }
        }

        public void ForwardMessage(TLMessageBase message)
        {
            if (ActiveItem == Files)
            {
                Files.ForwardMessage(message);
            }
            else if (ActiveItem == Links)
            {
                Links.ForwardMessage(message);
            }
            else if (ActiveItem == Music)
            {
                Music.ForwardMessage(message);
            }
        }
    }
}
