﻿using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Telegram.Controls.Extensions;

namespace TelegramClient.Animation.Navigation
{
    public class SwivelAnimator : AnimatorHelperBase
    {
        public override void Begin(Action completionAction)
        {
            if (this.PrepareElement(RootElement))
            {
                (RootElement.Projection as PlaneProjection).CenterOfRotationY = 0.5;
                Storyboard.Stop();
                base.SetTarget(RootElement);
            }

            base.Begin(completionAction);

            base.Begin(completionAction);
        }

        private bool PrepareElement(UIElement element)
        {
            if (element.GetPlaneProjection(true) == null)
            {
                return false;
            }
            return true;
        }
    }

    public class SwivelShowAnimator : SwivelAnimator
    {
        private static Storyboard _storyboard;

        public SwivelShowAnimator()
            : base()
        {
            if (_storyboard == null)
                _storyboard = XamlReader.Load(Storyboards.SwivelShowStoryboard) as Storyboard;

            Storyboard = _storyboard;
        }
    }

    public class SwivelHideAnimator : SwivelAnimator
    {
        private static Storyboard _storyboard;

        public SwivelHideAnimator()
            : base()
        {
            if (_storyboard == null)
                _storyboard = XamlReader.Load(Storyboards.SwivelHideStoryboard) as Storyboard;

            Storyboard = _storyboard;
        }
    }
}
