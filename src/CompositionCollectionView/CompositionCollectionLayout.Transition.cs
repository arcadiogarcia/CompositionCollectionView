// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#nullable enable
using System.Numerics;
using static CommunityToolkit.Labs.WinUI.AnimationConstants;

namespace CommunityToolkit.Labs.WinUI;
public abstract partial class CompositionCollectionLayout<TId, TItem> : ILayout, IDisposable where TId : notnull
{
    public void Activate(Panel panel)
    {
        var rootPanelVisual = InitializeRootContainer(panel);

        _uiRoot = new(
            panel,
            rootPanelVisual);

        Activate();

        Visual InitializeRootContainer(Panel root)
        {
            var rootContainer = ElementCompositionPreview.GetElementVisual(root);
            rootContainer.Size = new Vector2((float)root.ActualWidth, (float)root.ActualHeight);
            return rootContainer;
        }
    }

    private void Activate()
    {
        //The parent layout should only be accessible before we transition to the current layout,
        //once we activate the current layout we dispose and stop referencing it
        ParentLayout?.Dispose();
        ParentLayout = null;
        IsActive = true;

        foreach (var behavior in _behaviors)
        {
            behavior.Configure(this);
        }

        OnActivated();

        foreach (var behavior in _behaviors)
        {
            behavior.OnActivated();
        }
    }

    private void Deactivate()
    {
        OnDeactivated();

        IsActive = false;


        foreach (var behavior in _behaviors)
        {
            behavior.OnDeactivated();
        }
    }

    public T TransitionTo<T>(Func<CompositionCollectionLayout<TId, TItem>, T> factory, bool animateTransition = true) where T : CompositionCollectionLayout<TId, TItem>
    {
        var transitionStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var transferredCount = _elements.Count;
        var deactivateMs = 0.0;
        var factoryMs = 0.0;
        var activateMs = 0.0;
        var transferMs = 0.0;
        var layoutReplacedMs = 0.0;
        if (!IsActive)
        {
            throw new InvalidOperationException("TransitionTo can only be used in active layouts. You might have already transitioned away from this layout.");
        }

        var deactivateStopwatch = System.Diagnostics.Stopwatch.StartNew();
        Deactivate();
        deactivateStopwatch.Stop();
        deactivateMs = deactivateStopwatch.Elapsed.TotalMilliseconds;

        var factoryStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var newLayout = factory(this);
        factoryStopwatch.Stop();
        factoryMs = factoryStopwatch.Elapsed.TotalMilliseconds;

        foreach (var behavior in _behaviors)
        {
            newLayout.AddBehavior(behavior);
        }

        var activateStopwatch = System.Diagnostics.Stopwatch.StartNew();
        newLayout.Activate();
        activateStopwatch.Stop();
        activateMs = activateStopwatch.Elapsed.TotalMilliseconds;

        var transferStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var phaseReassignMs = 0.0;
        var phaseConfigureMs = 0.0;
        var phaseSnapshotMs = 0.0;
        var phaseStopAnimMs = 0.0;
        var phaseBuildKfaMs = 0.0;
        var phaseStartAnimMs = 0.0;
        var phaseScopedBatchMs = 0.0;
        var phaseUpdateElementMs = 0.0;
        var phaseOnElementsUpdatedMs = 0.0;
        TransferElements();
        transferStopwatch.Stop();
        transferMs = transferStopwatch.Elapsed.TotalMilliseconds;

        var layoutReplacedStopwatch = System.Diagnostics.Stopwatch.StartNew();
        LayoutReplaced?.Invoke(this, newLayout, animateTransition);
        layoutReplacedStopwatch.Stop();
        layoutReplacedMs = layoutReplacedStopwatch.Elapsed.TotalMilliseconds;

        transitionStopwatch.Stop();
        CompositionCollectionDiagnostics.Write(
            $"CCV.Transition {GetType().Name}->{newLayout.GetType().Name} transferred={transferredCount} animate={animateTransition} elapsedMs={transitionStopwatch.Elapsed.TotalMilliseconds:F2} deactivateMs={deactivateMs:F2} factoryMs={factoryMs:F2} activateMs={activateMs:F2} transferMs={transferMs:F2} layoutReplacedMs={layoutReplacedMs:F2} reassignMs={phaseReassignMs:F2} configureMs={phaseConfigureMs:F2} snapshotMs={phaseSnapshotMs:F2} stopAnimMs={phaseStopAnimMs:F2} buildKfaMs={phaseBuildKfaMs:F2} startAnimMs={phaseStartAnimMs:F2} scopedBatchMs={phaseScopedBatchMs:F2} updateElementMs={phaseUpdateElementMs:F2} onElementsUpdatedMs={phaseOnElementsUpdatedMs:F2}");

        return newLayout;

        void TransferElements()
        {
            var swReassign = System.Diagnostics.Stopwatch.StartNew();
            foreach (var (id, element) in _elements)
            {
                element.ReasignTo(newLayout);
                newLayout._elements.Add(id, element);
                CleanupElement(element);
                CleanupElementBehaviors(element);
            }
            swReassign.Stop();
            phaseReassignMs = swReassign.Elapsed.TotalMilliseconds;

            //Configure the animations after all the elements have been added to the new layout,
            //to allow elements to depend on each other
            foreach (var (id, element) in _elements)
            {
                var swConfig = System.Diagnostics.Stopwatch.StartNew();
                newLayout.ConfigureElement(element);
                newLayout.ConfigureElementBehaviors(element);
                swConfig.Stop();
                phaseConfigureMs += swConfig.Elapsed.TotalMilliseconds;

                var swSnapshot = System.Diagnostics.Stopwatch.StartNew();
                var currentPosition = GetElementPositionValue(element);
                var currentScale = GetElementScaleValue(element);
                var currentOrientation = GetElementOrientationValue(element);
                var currentOpacity = GetElementOpacityValue(element);
                swSnapshot.Stop();
                phaseSnapshotMs += swSnapshot.Elapsed.TotalMilliseconds;

                if (animateTransition && newLayout.GetElementTransitionEasingFunction(element) is ElementTransition transition)
                {
                    TaskCompletionSource<bool> tsc = new();
                    var swStop = System.Diagnostics.Stopwatch.StartNew();
                    newLayout.StopElementAnimation(element);
                    swStop.Stop();
                    phaseStopAnimMs += swStop.Elapsed.TotalMilliseconds;

                    var swBuild = System.Diagnostics.Stopwatch.StartNew();
                    var progressAnimation = Compositor.CreateScalarKeyFrameAnimation();
                    progressAnimation.Duration = TimeSpan.FromMilliseconds(transition.Length);
                    progressAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
                    progressAnimation.InsertKeyFrame(0, 0f, transition.EasingFunction);
                    progressAnimation.InsertKeyFrame(1, 1f, transition.EasingFunction);

                    var animProgressNode = new AnimatableScalarCompositionNode(Compositor);
                    swBuild.Stop();
                    phaseBuildKfaMs += swBuild.Elapsed.TotalMilliseconds;

                    var swStart = System.Diagnostics.Stopwatch.StartNew();
                    element.Visual.StartAnimation(Offset, ExpressionFunctions.Lerp(currentPosition, newLayout.GetElementPositionNode(element), animProgressNode.Reference));
                    var scale = newLayout.GetElementScaleNode(element);
                    element.Visual.StartAnimation(Scale, ExpressionFunctions.Lerp(new Vector3(currentScale), ExpressionFunctions.Vector3(scale, scale, scale), animProgressNode.Reference));
                    element.Visual.StartAnimation(AnimationConstants.Orientation, ExpressionFunctions.Slerp(currentOrientation, newLayout.GetElementOrientationNode(element), animProgressNode.Reference));
                    element.Visual.StartAnimation(Opacity, ExpressionFunctions.Lerp(currentOpacity, newLayout.GetElementOpacityNode(element), animProgressNode.Reference));
                    swStart.Stop();
                    phaseStartAnimMs += swStart.Elapsed.TotalMilliseconds;

                    var swBatch = System.Diagnostics.Stopwatch.StartNew();
                    var batch = Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    batch.Completed += (object _, CompositionBatchCompletedEventArgs _1) =>
                    {
                        if (newLayout._elements.ContainsKey(element.Id))
                        {
                            newLayout.ConfigureElementAnimation(element);
                        }
                        tsc.SetResult(true);

                        batch.Dispose();
                        animProgressNode.Dispose();
                        progressAnimation.Dispose();
                    };

                    animProgressNode.Animate(progressAnimation);

                    batch.End();
                    swBatch.Stop();
                    phaseScopedBatchMs += swBatch.Elapsed.TotalMilliseconds;
                }
                else
                {
                    newLayout.ConfigureElementAnimation(element);
                }

                var swUpdate = System.Diagnostics.Stopwatch.StartNew();
                newLayout.UpdateElement(element);
                swUpdate.Stop();
                phaseUpdateElementMs += swUpdate.Elapsed.TotalMilliseconds;
            }

            _elements.Clear();

            var swOnElementsUpdated = System.Diagnostics.Stopwatch.StartNew();
            newLayout.OnElementsUpdated();
            swOnElementsUpdated.Stop();
            phaseOnElementsUpdatedMs = swOnElementsUpdated.Elapsed.TotalMilliseconds;
        }
    }
}
