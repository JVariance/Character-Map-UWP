﻿using System;
using System.Linq;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers
{
    public class FluentAnimationHelper
    {
        private readonly VisualStateGroup _group;
        private FrameworkElement _pointerTarget = null;
        private FrameworkElement _pressedTarget = null;
        private VisualState _lastState = null;

        private bool _invalidPointer = false;
        private bool _invalidPressed = false;

        public FluentAnimationHelper(VisualStateGroup group)
        {
            _group = group;
            if (ResourceHelper.SupportFluentAnimation is false)
                return;

            _group.CurrentStateChanging += OnStateChanging;
        }

        private void OnStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (ResourceHelper.AllowAnimation is false
                || ResourceHelper.SupportFluentAnimation is false)
                return;

            // A hack to workaround an issue where StateChanging fires
            // twice on some buttons for (currently) unknown reasons
            if (e.NewState == _lastState)
                return;

            _lastState = e.NewState;

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"From {e.OldState?.Name} to {e.NewState.Name}");
#endif

            // 1. Handle "PointerOver"
            if (e.NewState is VisualState v
                && e.OldState is VisualState old
                && old.Name is "Normal" or "Disabled"
                && v.Name.StartsWith("PointerOver")
                && ResourceHelper.UsePointerOverAnimations
                && FluentAnimation.GetUsePointerOver(e.Control)
                && GetPointerTarget(e.Control) is FrameworkElement target)
            {
                Visual visual = target.EnableCompositionTranslation().GetElementVisual();
                float offset = (float)FluentAnimation.GetPointerOverOffset(e.Control);
                PlayPointerOver(visual, offset);
            }

            // 2. Handle "PressedDown"
            if (e.NewState is VisualState vp
                && vp.Name.StartsWith("Pressed")
                && e.OldState is VisualState ov
                && ov.Name.StartsWith("Pressed") is false
                && GetPressedTarget(e.Control) is FrameworkElement pressedTarget)
            {
                Visual vs = pressedTarget.GetElementVisual();
                float scale = (float)FluentAnimation.GetPointerDownScale(e.Control);
                vs.StartAnimation(CreatePointerDown(vs, scale));
            }
            // 3. Handle "PressedReleased"
            else if (e.OldState is VisualState oldP && oldP.Name.StartsWith("Pressed") is true
                    && e.NewState is VisualState ns && ns.Name.StartsWith("Pressed") is false
                    && GetPressedTarget(e.Control) is FrameworkElement f)
            {
                Visual vs = f.GetElementVisual();
                vs.StartAnimation(CreatePointerUp(vs));
            }
        }


        #region Animations

        public static CompositionAnimation CreatePointerUp(Visual v)
        {
            return v.Compositor.GetCached($"__FAPU", () =>
            {
                return v.CreateSpringVector3Animation(nameof(Visual.Scale))
                    .SetFinalValue(new(1))
                    .SetDampingRatio(0.4f);
            });
        }

        public static CompositionAnimation CreatePointerDown(Visual v, float scale)
        {
            CompositionFactory.StartCentering(v);
            return v.Compositor.GetCached($"__FAPD{scale}", () =>
            {
                return v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                    .AddScaleKeyFrame(1, scale, v.Compositor.GetLinearEase())
                    .SetDuration(0.1);
            });
        }

        public static void PlayPointerOver(Visual v, float offset)
        {
            // A Vector3Animation and a NaturalMotionAnimation can't be contained in 
            // the same CompositionAnimationGroup without breaking each other, so we
            // use a ScopedBatch to trigger the bounce back after the bounce away.
            v.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation,
                b =>
                {
                    v.StartAnimation(v.Compositor.GetCached($"__FAPO{offset}", () =>
                    {
                        return v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                            .AddKeyFrame(1, 0, offset, 0, v.Compositor.GetLinearEase())
                            .SetDuration(0.15);
                    }));
                },
                b =>
                {
                    v.StartAnimation(v.Compositor.GetCached($"__FAPOU", () =>
                    {
                        return v.CreateSpringVector3Animation(CompositionFactory.TRANSLATION)
                            .SetPeriod(0.04)
                            .SetFinalValue(new(0))
                            .SetDampingRatio(0.30f);
                    }));
                });
        }

        #endregion


        #region Targets

        public void InvalidatePressedTarget()
        {
            _invalidPressed = true;
        }

        public void InvalidatePointerTarget()
        {
            _invalidPointer = true;
        }

        internal void SetPointerTarget(object o)
        {
            if (o is FrameworkElement t)
                _pointerTarget = t;
            else
                _pointerTarget = null; // Will be updated next time GetPointerTarget is called
        }

        internal void SetPressedTarget(object o)
        {
            if (o is FrameworkElement t)
                _pressedTarget = t;
            else
                _pressedTarget = null; // Will be updated next time GetPressedTarget is called
        }

        private FrameworkElement GetTarget(Control c, DependencyProperty property, ref FrameworkElement store)
        {
            if (_group.GetValue(property) is FrameworkElement target)
                return target;

            if (_group.GetValue(property) is string name)
            {
                if (store is not null && store.Name == name)
                    return store;
                else
                    return c.GetDescendantsOfType<FrameworkElement>().FirstOrDefault(d => d.Name == name);
            }

            return null;
        }

        private FrameworkElement GetPointerTarget(Control c)
        {
            // Sync Properties - control value should take precedent over template value. 
            // Ideally we shouldn't need this as the AP setter should have done this but
            // my logic apparently fails somewhere...
            if (_invalidPointer && FluentAnimation.GetPointerOver(c) is object o 
                && o != FluentAnimation.GetPointerOver(_group))
            {
                _invalidPointer = false;
                FluentAnimation.SetPointerOver(_group, o);
            }

            return (_pointerTarget = GetTarget(c, FluentAnimation.PointerOverProperty, ref _pointerTarget));
        }

        private FrameworkElement GetPressedTarget(Control c)
        {
            // Sync Properties - control value should take precedent over template value. 
            // Ideally we shouldn't need this as the AP setter should have done this but
            // my logic apparently fails somewhere...
            if (_invalidPressed && FluentAnimation.GetPressed(c) is object o
                && o != FluentAnimation.GetPressed(_group))
            {
                _invalidPressed = false;
                FluentAnimation.SetPressed(_group, o);
            }

            return (_pressedTarget = GetTarget(c, FluentAnimation.PressedProperty, ref _pressedTarget));
        }

        #endregion
    }

    [Bindable]
    public class FluentAnimation
    {
        #region Helper

        public static FluentAnimationHelper GetHelper(DependencyObject obj) 
            => (FluentAnimationHelper)obj.GetValue(HelperProperty);

        private static void SetHelper(DependencyObject obj, FluentAnimationHelper value) 
            => obj.SetValue(HelperProperty, value);

        public static readonly DependencyProperty HelperProperty =
            DependencyProperty.RegisterAttached("Helper", typeof(FluentAnimationHelper), typeof(FluentAnimation), new PropertyMetadata(null));


        /// <summary>
        /// Ensures a single FluentAnimationHelper per VisualStateGroup
        /// </summary>
        /// <param name="group"></param>
        private static FluentAnimationHelper EnsureHelper(VisualStateGroup group)
        {
            if (!(GetHelper(group) is FluentAnimationHelper helper))
            {
                helper = new(group);
                SetHelper(group, helper);
            }

            return helper;
        }

        /// <summary>
        /// Passes down values from a Control to it's internal templated VisualStateGroup
        /// </summary>
        static void TryHook(Control c, Action<VisualStateGroup, object> prop, object value)
        {
            if (ResourceHelper.SupportFluentAnimation is false)
                return;

            if (c.IsLoaded)
                Hook(c, prop, value);
            else
            {
                c.Loaded -= C_Loaded;
                c.Loaded += C_Loaded;

                void C_Loaded(object sender, RoutedEventArgs e)
                {
                    if (sender is Control s)
                    {
                        s.Loaded -= C_Loaded;
                        Hook(s, prop, value);
                    }
                }
            }

            static void Hook(Control control, Action<VisualStateGroup, object> p, object v)
            {
                if (control.GetVisualStateGroup("CommonStates") is VisualStateGroup group)
                    p(group, v);
                else
                {
                    if (VisualTreeHelperExtensions.GetImplementationRoot(control) is null)
                    {
                        control.SizeChanged -= Control_SizeChanged;
                        control.SizeChanged += Control_SizeChanged;

                        void Control_SizeChanged(object sender, SizeChangedEventArgs e)
                        {
                            if (e.NewSize.IsEmpty)
                                return;

                            control.SizeChanged -= Control_SizeChanged;
                            Hook(control, p, v);
                        }
                    }
                }
            }
        }

        #endregion

        #region PointerDownScale

        public static double GetPointerDownScale(DependencyObject obj)
            => (double)obj.GetValue(PointerDownScaleProperty);

        public static void SetPointerDownScale(DependencyObject obj, double value)
            => obj.SetValue(PointerDownScaleProperty, value);

        public static readonly DependencyProperty PointerDownScaleProperty =
            DependencyProperty.RegisterAttached("PointerDownScale", typeof(double), typeof(FluentAnimation), new PropertyMetadata(0.94d));

        #endregion

        #region Pressed

        public static object GetPressed(DependencyObject obj) 
            => (object)obj.GetValue(PressedProperty);

        public static void SetPressed(DependencyObject obj, object value) 
            => obj.SetValue(PressedProperty, value);

        public static readonly DependencyProperty PressedProperty =
            DependencyProperty.RegisterAttached("Pressed", typeof(object), typeof(FluentAnimation), new PropertyMetadata(null, (d, a) =>
            {
                if (d is VisualStateGroup group)
                {
                    if (GetHelper(group) is FluentAnimationHelper helper)
                        helper.SetPressedTarget(a.NewValue);
                    else if (a.NewValue is not null)
                        EnsureHelper(group);
                }
                else if (d is Control c)
                {
                    TryHook(c, (g, v) =>
                    {
                        EnsureHelper(g).InvalidatePressedTarget();
                    }, a.NewValue);
                }
            }));

        #endregion

        #region PointerOver

        public static object GetPointerOver(DependencyObject obj)
        {
            return (object)obj.GetValue(PointerOverProperty);
        }

        public static void SetPointerOver(DependencyObject obj, object value)
        {
            obj.SetValue(PointerOverProperty, value);
        }

        public static readonly DependencyProperty PointerOverProperty =
            DependencyProperty.RegisterAttached("PointerOver", typeof(object), typeof(FluentAnimation), new PropertyMetadata(null, (d, a) =>
            {
                // Expects either a direct FrameworkElement (best for performance)
                // or name of a Template/VisualTree child
                if (d is VisualStateGroup group)
                {
                    if (GetHelper(group) is FluentAnimationHelper helper)
                        helper.SetPointerTarget(a.NewValue);
                    else if (a.NewValue is not null)
                        EnsureHelper(group);
                }
                else if (d is Control c)
                {
                    TryHook(c, (g, v) =>
                    {
                        EnsureHelper(g).InvalidatePointerTarget();
                    }, a.NewValue);
                }
            }));

        #endregion

        #region PointerOverOffset

        public static double GetPointerOverOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(PointerOverOffsetProperty);
        }

        public static void SetPointerOverOffset(DependencyObject obj, double value)
        {
            obj.SetValue(PointerOverOffsetProperty, value);
        }

        public static readonly DependencyProperty PointerOverOffsetProperty =
            DependencyProperty.RegisterAttached("PointerOverOffset", typeof(double), typeof(FluentAnimation), new PropertyMetadata(-2d));

        #endregion

        #region UsePointerOver

        public static bool GetUsePointerOver(DependencyObject obj)
            => (bool)obj.GetValue(UsePointerOverProperty);

        public static void SetUsePointerOver(DependencyObject obj, bool value)
            => obj.SetValue(UsePointerOverProperty, value);

        public static readonly DependencyProperty UsePointerOverProperty =
            DependencyProperty.RegisterAttached("UsePointerOver", typeof(bool), typeof(FluentAnimation), new PropertyMetadata(true));

        #endregion
    }
}
