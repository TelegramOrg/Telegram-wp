﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Telegram.Controls;
using Telegram.Controls.Extensions;
using TelegramClient.Animation.Navigation;
using TelegramClient.Helpers;
using TelegramClient.ViewModels.Search;

namespace TelegramClient.Views.Search
{
    public partial class SearchContactsView : IDisposable
    {
        private readonly IDisposable _keyPressSubscription;

        public SearchContactsViewModel ViewModel
        {
            get { return DataContext as SearchContactsViewModel; }
        }

        public SearchContactsView()
        {
            InitializeComponent();

            AnimationContext = LayoutRoot;

            var keyPressEvents = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                keh => { SearchBox.TextChanged += keh; },
                keh => { SearchBox.TextChanged -= keh; });

            _keyPressSubscription = keyPressEvents
                .Throttle(TimeSpan.FromSeconds(0.10))
                .ObserveOnDispatcher()
                .Subscribe(e => ViewModel.Search());

            Loaded += (o, e) =>
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            };

            Unloaded += (o, e) =>
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsWorking))
            {
                if (!ViewModel.IsWorking)
                {
                    RestoreTapedItem();
                }
            }
        }

        private void RestoreTapedItem()
        {
            if (_lastTapedItem != null)
            {
                if (_lastTapedItem != null)
                {
                    _lastStoryboard.Stop();
                }

                _lastTapedItem.Opacity = 1.0;
                var compositeTransform = _lastTapedItem.RenderTransform as CompositeTransform;
                if (compositeTransform != null)
                {
                    compositeTransform.TranslateX = 0.0;
                    compositeTransform.TranslateY = 0.0;
                }
            }
        }

        protected override AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        {
            if (animationType == AnimationType.NavigateForwardIn
                || animationType == AnimationType.NavigateBackwardIn)
            {
                return new SlideUpAnimator { RootElement = LayoutRoot };
            }

            return null;
        }

        protected override void AnimationsComplete(AnimationType animationType)
        {
            if (animationType == AnimationType.NavigateForwardIn)
            {
                if (string.IsNullOrEmpty(ViewModel.Text))
                {
                    SearchBox.Focus();
                }
            }

            base.AnimationsComplete(animationType);
        }

        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            if (_lastTapedItem != null)
            {
                _lastTapedItem.Opacity = 1.0;
                var compositeTransform = _lastTapedItem.RenderTransform as CompositeTransform;
                if (compositeTransform != null)
                {
                    compositeTransform.TranslateX = 0.0;
                    compositeTransform.TranslateY = 0.0;
                }
            }

            if (ViewModel.Confirmation.IsOpen)
            {
                e.Cancel = true;
                return;
            }

            if (ViewModel.NavigateToSecretChat && ViewModel.Contact != null)
            {
                ViewModel.CancelSecretChat();
                e.Cancel = true;
                return;
            }

            base.OnBackKeyPress(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (!e.Cancel && e.Uri.OriginalString != "app://external/")
            {
                Items.DetachPropertyListener();

                var storyboard = new Storyboard();

                var translateAnimation = new DoubleAnimationUsingKeyFrames();
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 0.0 });
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 150.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                Storyboard.SetTarget(translateAnimation, LayoutRoot);
                Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                storyboard.Children.Add(translateAnimation);

                var opacityAnimation = new DoubleAnimationUsingKeyFrames();
                opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 1.0 });
                opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                Storyboard.SetTarget(opacityAnimation, LayoutRoot);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
                storyboard.Children.Add(opacityAnimation);

                storyboard.Begin();
            }
        }

        public void Dispose()
        {
            _keyPressSubscription.Dispose();
        }

        private FrameworkElement _lastTapedItem;

        private Storyboard _lastStoryboard;

        private void MainItemGrid_OnTextBlockTap(object sender, GestureEventArgs args)
        {
            MainItem_OnTapCommon<TextBlock>(sender, args);
        }

        private void MainItemGrid_OnHighlightingTextBlockTap(object sender, GestureEventArgs args)
        {
            MainItem_OnTapCommon<HighlightingTextBlock>(sender, args);
        }

        private void MainItem_OnTapCommon<T>(object sender, GestureEventArgs args) 
            where T : FrameworkElement
        {
            if (!ViewModel.NavigateToDialogDetails && !ViewModel.NavigateToSecretChat) return;

            _lastTapedItem = sender as FrameworkElement;

            if (_lastTapedItem != null)
            {
                //foreach (var descendant in _lastTapedItem.GetVisualDescendants().OfType<T>())
                //{
                //    if (GetIsAnimationTarget(descendant))
                //    {
                //        _lastTapedItem = descendant;
                //        break;
                //    }
                //}

                if (!(_lastTapedItem.RenderTransform is CompositeTransform))
                {
                    _lastTapedItem.RenderTransform = new CompositeTransform();
                }

                var tapedItemContainer = _lastTapedItem.FindParentOfType<ListBoxItem>();
                if (tapedItemContainer != null)
                {
                    tapedItemContainer = tapedItemContainer.FindParentOfType<ListBoxItem>();
                }

                _lastStoryboard = ShellView.StartContinuumForwardOutAnimation(_lastTapedItem, tapedItemContainer);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_lastTapedItem != null)
            {
                var transform = _lastTapedItem.RenderTransform as CompositeTransform;
                if (transform != null)
                {
                    transform.TranslateX = 0.0;
                    transform.TranslateY = 0.0;
                }
                _lastTapedItem.Opacity = 1.0;
            }

            base.OnNavigatedTo(e);
        }

        private void Items_OnScrollingStateChanged(object sender, ScrollingStateChangedEventArgs e)
        {
            if (e.NewValue)
            {
                var focusElement = FocusManager.GetFocusedElement();
                if (focusElement == SearchBox)
                {
                    Self.Focus();
                }
            }
        }
    }
}