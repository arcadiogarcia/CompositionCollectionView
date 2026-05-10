// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#nullable enable
using System.Numerics;
using static CommunityToolkit.Labs.WinUI.AnimationConstants;


namespace CommunityToolkit.Labs.WinUI;
public abstract partial class CompositionCollectionLayout<TId, TItem> : ILayout, IDisposable where TId : notnull
{

    bool _isUpdatingSource = false;

    private record SourceUpdate(IDictionary<TId, TItem> UpdatedElements, TaskCompletionSource<bool> TaskCompletion, bool Animated);
    Queue<SourceUpdate> _pendingSourceUpdates = new();

    public async Task UpdateSource(IDictionary<TId, TItem> source, bool animate)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!_isUpdatingSource)
        {
            _isUpdatingSource = true;
            ProcessSourceUpdate(source, tcs, animate);
        }
        else
        {
            _pendingSourceUpdates.Enqueue(new SourceUpdate(source, tcs, animate));
        }

        await tcs.Task;
    }

    private HashSet<AnimatableScalarCompositionNode> _ongoingSourceUpdateAnimation = new();

    public async void ProcessSourceUpdate(IDictionary<TId, TItem> updatedElements, TaskCompletionSource<bool> taskCompletion, bool animate)
    {
        var updateStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var destroyedCount = 0;
        var updatedCount = 0;
        var instantiatedCount = 0;
        var reusedCount = 0;
        var updateExistingMs = 0.0;
        var instantiateMs = 0.0;
        var destroyMs = 0.0;
        var poolAcquireMs = 0.0;
        var poolRetireMs = 0.0;
        var elementFactoryMs = 0.0;
        var childAddMs = 0.0;
        var elementRefAllocMs = 0.0;
        var poolRestoreMs = 0.0;
        var elementsDictAddMs = 0.0;
        var updateDataMs = 0.0;
        var configureMs = 0.0;
        var configureAnimationMs = 0.0;
        var updateElementMs = 0.0;
        var onElementsUpdatedMs = 0.0;
        List<Task<bool>> elementUpdateTask = new();

        HashSet<TId> processedElements = new();

        foreach (var (id, element) in _elements.ToArray())
        {
            if (!updatedElements.ContainsKey(id))
            {
                var stageStopwatch = System.Diagnostics.Stopwatch.StartNew();
                DestroyElement(element, id);
                stageStopwatch.Stop();
                destroyMs += stageStopwatch.Elapsed.TotalMilliseconds;
                destroyedCount++;
            }
            else
            {
                var stageStopwatch = System.Diagnostics.Stopwatch.StartNew();
                UpdateAndTransitionElement(element, updatedElements[id]);
                stageStopwatch.Stop();
                updateExistingMs += stageStopwatch.Elapsed.TotalMilliseconds;
                processedElements.Add(id);
                updatedCount++;
            }
        }
        foreach (var (id, model) in updatedElements)
        {
            if (processedElements.Contains(id))
            {
                continue;
            }
            var stageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            InstantiateElement(id, model);
            stageStopwatch.Stop();
            instantiateMs += stageStopwatch.Elapsed.TotalMilliseconds;
        }

        var onElementsUpdatedStopwatch = System.Diagnostics.Stopwatch.StartNew();
        OnElementsUpdated();
        onElementsUpdatedStopwatch.Stop();
        onElementsUpdatedMs += onElementsUpdatedStopwatch.Elapsed.TotalMilliseconds;
        updateStopwatch.Stop();
        CompositionCollectionDiagnostics.Write(
            $"CCV.UpdateSource layout={GetType().Name} source={updatedElements.Count} active={_elements.Count} updated={updatedCount} instantiated={instantiatedCount} reused={reusedCount} destroyed={destroyedCount} elapsedMs={updateStopwatch.Elapsed.TotalMilliseconds:F2} updateExistingMs={updateExistingMs:F2} instantiateMs={instantiateMs:F2} destroyMs={destroyMs:F2} poolAcquireMs={poolAcquireMs:F2} poolRetireMs={poolRetireMs:F2} factoryMs={elementFactoryMs:F2} childAddMs={childAddMs:F2} elementRefAllocMs={elementRefAllocMs:F2} poolRestoreMs={poolRestoreMs:F2} elementsDictAddMs={elementsDictAddMs:F2} updateDataMs={updateDataMs:F2} configureMs={configureMs:F2} configureAnimationMs={configureAnimationMs:F2} updateElementMs={updateElementMs:F2} onElementsUpdatedMs={onElementsUpdatedMs:F2}");

        taskCompletion.SetResult(true);

        if (elementUpdateTask.Any())
        {
            await Task.WhenAll(elementUpdateTask);
        }

        if (_pendingSourceUpdates.Count > 0)
        {
            var update = _pendingSourceUpdates.Dequeue();
            ProcessSourceUpdate(update.UpdatedElements, update.TaskCompletion, update.Animated);
        }
        else
        {
            _isUpdatingSource = false;
        }

        void InstantiateElement(TId id, TItem item)
        {
            FrameworkElement element;
            var context = CreateElementPoolContext();
            FrameworkElement? pooledElement = null;
            var poolAcquireStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var reused = ElementPoolPolicy?.TryAcquire(id, item, context, out pooledElement) == true && pooledElement is not null;
            poolAcquireStopwatch.Stop();
            poolAcquireMs += poolAcquireStopwatch.Elapsed.TotalMilliseconds;
            if (reused)
            {
                reusedCount++;
                element = pooledElement!;
                if (element.Parent is null)
                {
                    var swAdd = System.Diagnostics.Stopwatch.StartNew();
                    RootPanel.Children.Add(element);
                    swAdd.Stop();
                    childAddMs += swAdd.Elapsed.TotalMilliseconds;
                }
                else if (!ReferenceEquals(element.Parent, RootPanel))
                {
                    throw new InvalidOperationException("A pooled element returned by the pool policy is parented to a different panel.");
                }
            }
            else
            {
                var elementFactoryStopwatch = System.Diagnostics.Stopwatch.StartNew();
                element = ElementFactory(id);
                elementFactoryStopwatch.Stop();
                elementFactoryMs += elementFactoryStopwatch.Elapsed.TotalMilliseconds;
                var swAdd2 = System.Diagnostics.Stopwatch.StartNew();
                RootPanel.Children.Add(element);
                swAdd2.Stop();
                childAddMs += swAdd2.Elapsed.TotalMilliseconds;
            }
            instantiatedCount++;

            var swRefAlloc = System.Diagnostics.Stopwatch.StartNew();
            var elementReference = new ElementReference<TId, TItem>(id, item, element,/* source, tracker, trackerOwner,*/ this);
            swRefAlloc.Stop();
            elementRefAllocMs += swRefAlloc.Elapsed.TotalMilliseconds;
            if (reused)
            {
                var swRestore = System.Diagnostics.Stopwatch.StartNew();
                ElementPoolPolicy?.Restore(elementReference, item, CreateElementPoolContext());
                swRestore.Stop();
                poolRestoreMs += swRestore.Elapsed.TotalMilliseconds;
            }
            var updateDataStopwatch = System.Diagnostics.Stopwatch.StartNew();
            UpdateElementData(elementReference);
            updateDataStopwatch.Stop();
            updateDataMs += updateDataStopwatch.Elapsed.TotalMilliseconds;
            var swDictAdd = System.Diagnostics.Stopwatch.StartNew();
            _elements[id] = elementReference;
            swDictAdd.Stop();
            elementsDictAddMs += swDictAdd.Elapsed.TotalMilliseconds;

            var configureStopwatch = System.Diagnostics.Stopwatch.StartNew();
            ConfigureElement(elementReference);
            ConfigureElementBehaviors(elementReference);
            configureStopwatch.Stop();
            configureMs += configureStopwatch.Elapsed.TotalMilliseconds;

            var configureAnimationStopwatch = System.Diagnostics.Stopwatch.StartNew();
            ConfigureElementAnimation(elementReference);
            configureAnimationStopwatch.Stop();
            configureAnimationMs += configureAnimationStopwatch.Elapsed.TotalMilliseconds;
            var updateElementStopwatch = System.Diagnostics.Stopwatch.StartNew();
            UpdateElement(elementReference);
            updateElementStopwatch.Stop();
            updateElementMs += updateElementStopwatch.Elapsed.TotalMilliseconds;
        }

        void DestroyElement(ElementReference<TId, TItem> element, TId id)
        {
            StopElementAnimation(element);
            CleanupElement(element);
            CleanupElementBehaviors(element);
            var policy = ElementPoolPolicy;
            var context = CreateElementPoolContext();
            var poolRetireStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var retired = policy?.TryRetire(element, ElementRemovalReason.SourceUpdate, context) == true;
            poolRetireStopwatch.Stop();
            poolRetireMs += poolRetireStopwatch.Elapsed.TotalMilliseconds;
            if (!retired)
            {
                if (policy is not null)
                {
                    policy.Destroy(element, ElementRemovalReason.SourceUpdate, context);
                }
                else
                {
                    RootPanel.Children.Remove(element.Container);
                }
            }
            _elements.Remove(id);
            element.Dispose();
            policy?.Trim(CreateElementPoolContext());
        }

        void UpdateAndTransitionElement(ElementReference<TId, TItem> element, TItem newData)
        {
            if (animate && GetElementTransitionEasingFunction(element) is ElementTransition transition)
            {
                var currentPosition = GetElementPositionValue(element);
                var currentScale = GetElementScaleValue(element);
                var currentOrientation = GetElementOrientationValue(element);
                var currentOpacity = GetElementOpacityValue(element);

                TaskCompletionSource<bool> tsc = new();
                StopElementAnimation(element);

                element.Model = newData;
                var updateDataStopwatch = System.Diagnostics.Stopwatch.StartNew();
                UpdateElementData(element);
                updateDataStopwatch.Stop();
                updateDataMs += updateDataStopwatch.Elapsed.TotalMilliseconds;

                var progressAnimation = Compositor.CreateScalarKeyFrameAnimation();
                progressAnimation.Duration = TimeSpan.FromMilliseconds(transition.Length);
                progressAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
                progressAnimation.InsertKeyFrame(0, 0f, transition.EasingFunction);
                progressAnimation.InsertKeyFrame(1, 1f, transition.EasingFunction);

                var animProgressNode = new AnimatableScalarCompositionNode(Compositor);
                _ongoingSourceUpdateAnimation.Add(animProgressNode);

                element.Visual.StartAnimation(Offset, ExpressionFunctions.Lerp(currentPosition, GetElementPositionNode(element), animProgressNode.Reference));
                var scale = GetElementScaleNode(element);
                element.Visual.StartAnimation(Scale, ExpressionFunctions.Lerp(new Vector3(currentScale), ExpressionFunctions.Vector3(scale, scale, scale), animProgressNode.Reference));
                element.Visual.StartAnimation(AnimationConstants.Orientation, ExpressionFunctions.Slerp(currentOrientation, GetElementOrientationNode(element), animProgressNode.Reference));
                element.Visual.StartAnimation(Opacity, ExpressionFunctions.Lerp(currentOpacity, GetElementOpacityNode(element), animProgressNode.Reference));

                var batch = Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (object _, CompositionBatchCompletedEventArgs _1) =>
                {
                    if (IsActive)
                    {
                        ConfigureElementAnimation(element);
                    }
                    tsc.SetResult(true);

                    batch.Dispose();
                    animProgressNode.Dispose();
                    progressAnimation.Dispose();

                    _ongoingSourceUpdateAnimation.Remove(animProgressNode);
                };

                animProgressNode.Animate(progressAnimation);

                batch.End();

                elementUpdateTask.Add(tsc.Task);
            }
            else
            {
                element.Model = newData;
                var updateDataStopwatch = System.Diagnostics.Stopwatch.StartNew();
                UpdateElementData(element);
                updateDataStopwatch.Stop();
                updateDataMs += updateDataStopwatch.Elapsed.TotalMilliseconds;
            }

            var updateElementStopwatch = System.Diagnostics.Stopwatch.StartNew();
            UpdateElement(element);
            updateElementStopwatch.Stop();
            updateElementMs += updateElementStopwatch.Elapsed.TotalMilliseconds;
            processedElements.Add(element.Id);
        }
    }
}
