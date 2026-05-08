using SANJET.Core.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SANJET.UI.Views.Windows
{
    public partial class LoadingWindow : Window
    {
        private static readonly TimeSpan CompletionAnimationDuration = TimeSpan.FromSeconds(3.5);

        public LoadingWindowViewModel? ViewModel { get; private set; }

        public LoadingWindow()
        {
            InitializeComponent();
        }

        public LoadingWindow(LoadingWindowViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        public Task PlayCompletionAnimationAsync()
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var storyboard = CreateCompletionStoryboard();

            storyboard.Completed += (_, _) => completionSource.TrySetResult();
            storyboard.Begin(this);

            return completionSource.Task;
        }

        private Storyboard CreateCompletionStoryboard()
        {
            var storyboard = new Storyboard();
            var dotRotations = new[]
            {
                (Transform: DotRotate1, StartAngle: 0d),
                (Transform: DotRotate2, StartAngle: 45d),
                (Transform: DotRotate3, StartAngle: 90d),
                (Transform: DotRotate4, StartAngle: 135d),
                (Transform: DotRotate5, StartAngle: 180d),
                (Transform: DotRotate6, StartAngle: 225d),
                (Transform: DotRotate7, StartAngle: 270d),
                (Transform: DotRotate8, StartAngle: 315d)
            };

            foreach (var (transform, startAngle) in dotRotations)
            {
                var rotationAnimation = new DoubleAnimation
                {
                    From = startAngle,
                    To = startAngle + 360,
                    Duration = CompletionAnimationDuration
                };

                Storyboard.SetTarget(rotationAnimation, transform);
                Storyboard.SetTargetProperty(rotationAnimation, new PropertyPath(RotateTransform.AngleProperty));
                storyboard.Children.Add(rotationAnimation);
            }

            var dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5, Dot6, Dot7, Dot8 };

            foreach (var dot in dots)
            {
                var bounceAnimation = new DoubleAnimation
                {
                    From = 10,
                    To = 32,
                    Duration = TimeSpan.FromMilliseconds(CompletionAnimationDuration.TotalMilliseconds / 2),
                    AutoReverse = true
                };

                Storyboard.SetTarget(bounceAnimation, dot);
                Storyboard.SetTargetProperty(bounceAnimation, new PropertyPath(Canvas.TopProperty));
                storyboard.Children.Add(bounceAnimation);
            }

            return storyboard;
        }
    }
}
