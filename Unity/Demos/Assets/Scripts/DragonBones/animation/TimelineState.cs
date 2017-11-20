﻿using System;
using System.Collections.Generic;

namespace DragonBones
{
    /**
     * @internal
     * @private
     */
    internal class ActionTimelineState : TimelineState
    {
        private void _OnCrossFrame(int frameIndex)
        {
            var eventDispatcher = this._armature.proxy;
            if (this._animationState.actionEnabled)
            {
                var frameOffset = this._animationData.frameOffset + this._timelineArray[(this._timelineData as TimelineData).offset + (int)BinaryOffset.TimelineFrameOffset + frameIndex];
                var actionCount = this._frameArray[frameOffset + 1];
                var actions = this._armature.armatureData.actions;

                for (var i = 0; i < actionCount; ++i)
                {
                    var actionIndex = this._frameArray[frameOffset + 2 + i];
                    var action = actions[actionIndex];

                    if (action.type == ActionType.Play)
                    {
                        if (action.slot != null)
                        {
                            var slot = this._armature.GetSlot(action.slot.name);
                            if (slot != null)
                            {
                                var childArmature = slot.childArmature;
                                if (childArmature != null)
                                {
                                    childArmature._BufferAction(action, true);
                                }
                            }
                        }
                        else if (action.bone != null)
                        {
                            foreach (var slot in this._armature.GetSlots())
                            {
                                var childArmature = slot.childArmature;
                                if (childArmature != null && slot.parent.boneData == action.bone)
                                {
                                    childArmature._BufferAction(action, true);
                                }
                            }
                        }
                        else
                        {
                            this._armature._BufferAction(action, true);
                        }
                    }
                    else
                    {
                        var eventType = action.type == ActionType.Frame ? EventObject.FRAME_EVENT : EventObject.SOUND_EVENT;
                        if (action.type == ActionType.Sound || eventDispatcher.HasDBEventListener(eventType))
                        {
                            var eventObject = BaseObject.BorrowObject<EventObject>();
                            // eventObject.time = this._frameArray[frameOffset] * this._frameRateR; // Precision problem
                            eventObject.time = (float)this._frameArray[frameOffset] / (float)this._frameRate;
                            eventObject.type = eventType;
                            eventObject.name = action.name;
                            eventObject.data = action.data;
                            eventObject.armature = this._armature;
                            eventObject.animationState = this._animationState;

                            if (action.bone != null)
                            {
                                eventObject.bone = this._armature.GetBone(action.bone.name);
                            }

                            if (action.slot != null)
                            {
                                eventObject.slot = this._armature.GetSlot(action.slot.name);
                            }

                            this._armature._dragonBones.BufferEvent(eventObject);
                        }
                    }
                }
            }
        }

        protected override void _OnArriveAtFrame() { }
        protected override void _OnUpdateFrame() { }

        public override void Update(float passedTime)
        {
            var prevState = this.playState;
            var prevPlayTimes = this.currentPlayTimes;
            var prevTime = this.currentTime;

            if (this._SetCurrentTime(passedTime))
            {
                var eventDispatcher = this._armature.proxy;
                if (prevState < 0)
                {
                    if (this.playState != prevState)
                    {
                        if (this._animationState.displayControl && this._animationState.resetToPose)
                        {
                            // Reset zorder to pose.
                            this._armature._SortZOrder(null, 0);
                        }

                        prevPlayTimes = this.currentPlayTimes;

                        if (eventDispatcher.HasDBEventListener(EventObject.START))
                        {
                            var eventObject = BaseObject.BorrowObject<EventObject>();
                            eventObject.type = EventObject.START;
                            eventObject.armature = this._armature;
                            eventObject.animationState = this._animationState;
                            this._armature._dragonBones.BufferEvent(eventObject);
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                var isReverse = this._animationState.timeScale < 0.0f;
                EventObject loopCompleteEvent = null;
                EventObject completeEvent = null;

                if (this.currentPlayTimes != prevPlayTimes)
                {
                    if (eventDispatcher.HasDBEventListener(EventObject.LOOP_COMPLETE))
                    {
                        loopCompleteEvent = BaseObject.BorrowObject<EventObject>();
                        loopCompleteEvent.type = EventObject.LOOP_COMPLETE;
                        loopCompleteEvent.armature = this._armature;
                        loopCompleteEvent.animationState = this._animationState;
                    }

                    if (this.playState > 0)
                    {
                        if (eventDispatcher.HasDBEventListener(EventObject.COMPLETE))
                        {
                            completeEvent = BaseObject.BorrowObject<EventObject>();
                            completeEvent.type = EventObject.COMPLETE;
                            completeEvent.armature = this._armature;
                            completeEvent.animationState = this._animationState;
                        }
                    }
                }

                if (this._frameCount > 1)
                {
                    var timelineData = this._timelineData as TimelineData;
                    var timelineFrameIndex = (uint)Math.Floor(this.currentTime * this._frameRate); // uint
                    var frameIndex = (int)this._frameIndices[timelineData.frameIndicesOffset + timelineFrameIndex];
                    if (this._frameIndex != frameIndex)
                    {
                        // Arrive at frame.                   
                        var crossedFrameIndex = this._frameIndex;
                        this._frameIndex = frameIndex;
                        if (this._timelineArray != null)
                        {
                            this._frameOffset = this._animationData.frameOffset + this._timelineArray[timelineData.offset + (int)BinaryOffset.TimelineFrameOffset + this._frameIndex];
                            if (isReverse)
                            {
                                if (crossedFrameIndex < 0)
                                {
                                    var prevFrameIndex = (int)Math.Floor(prevTime * this._frameRate);
                                    crossedFrameIndex = (int)this._frameIndices[timelineData.frameIndicesOffset + prevFrameIndex];
                                    if (this.currentPlayTimes == prevPlayTimes)
                                    {
                                        // Start.
                                        if (crossedFrameIndex == frameIndex)
                                        { // Uncrossed.
                                            crossedFrameIndex = -1;
                                        }
                                    }
                                }

                                while (crossedFrameIndex >= 0)
                                {
                                    var frameOffset = this._animationData.frameOffset + this._timelineArray[timelineData.offset + (int)BinaryOffset.TimelineFrameOffset + crossedFrameIndex];
                                    // const framePosition = this._frameArray[frameOffset] * this._frameRateR; // Precision problem
                                    var framePosition = (float)this._frameArray[frameOffset] / (float)this._frameRate;

                                    if (this._position <= framePosition && framePosition <= this._position + this._duration)
                                    {
                                        // Support interval play.
                                        this._OnCrossFrame(crossedFrameIndex);
                                    }

                                    if (loopCompleteEvent != null && crossedFrameIndex == 0)
                                    {
                                        // Add loop complete event after first frame.
                                        this._armature._dragonBones.BufferEvent(loopCompleteEvent);
                                        loopCompleteEvent = null;
                                    }

                                    if (crossedFrameIndex > 0)
                                    {
                                        crossedFrameIndex--;
                                    }
                                    else
                                    {
                                        crossedFrameIndex = (int)this._frameCount - 1;
                                    }

                                    if (crossedFrameIndex == frameIndex)
                                    {
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if (crossedFrameIndex < 0)
                                {
                                    var prevFrameIndex = (int)Math.Floor(prevTime * this._frameRate);
                                    crossedFrameIndex = (int)this._frameIndices[timelineData.frameIndicesOffset + prevFrameIndex];
                                    var frameOffset = this._animationData.frameOffset + this._timelineArray[timelineData.offset + (int)BinaryOffset.TimelineFrameOffset + crossedFrameIndex];
                                    // const framePosition = this._frameArray[frameOffset] * this._frameRateR; // Precision problem
                                    var framePosition = (float)this._frameArray[frameOffset] / (float)this._frameRate;
                                    if (this.currentPlayTimes == prevPlayTimes)
                                    {
                                        // Start.
                                        if (prevTime <= framePosition)
                                        {
                                            // Crossed.
                                            if (crossedFrameIndex > 0)
                                            {
                                                crossedFrameIndex--;
                                            }
                                            else
                                            {
                                                crossedFrameIndex = (int)this._frameCount - 1;
                                            }
                                        }
                                        else if (crossedFrameIndex == frameIndex)
                                        {
                                            // Uncrossed.
                                            crossedFrameIndex = -1;
                                        }
                                    }
                                }

                                while (crossedFrameIndex >= 0)
                                {
                                    if (crossedFrameIndex < this._frameCount - 1)
                                    {
                                        crossedFrameIndex++;
                                    }
                                    else
                                    {
                                        crossedFrameIndex = 0;
                                    }

                                    var frameOffset = this._animationData.frameOffset + this._timelineArray[timelineData.offset + (int)BinaryOffset.TimelineFrameOffset + crossedFrameIndex];
                                    // const framePosition = this._frameArray[frameOffset] * this._frameRateR; // Precision problem
                                    var framePosition = (float)this._frameArray[frameOffset] / (float)this._frameRate;
                                    if (this._position <= framePosition && framePosition <= this._position + this._duration)
                                    {
                                        // Support interval play.
                                        this._OnCrossFrame(crossedFrameIndex);
                                    }

                                    if (loopCompleteEvent != null && crossedFrameIndex == 0)
                                    {
                                        // Add loop complete event before first frame.
                                        this._armature._dragonBones.BufferEvent(loopCompleteEvent);
                                        loopCompleteEvent = null;
                                    }

                                    if (crossedFrameIndex == frameIndex)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (this._frameIndex < 0)
                {
                    this._frameIndex = 0;
                    if (this._timelineData != null)
                    {
                        this._frameOffset = this._animationData.frameOffset + this._timelineArray[this._timelineData.offset + (int)BinaryOffset.TimelineFrameOffset];
                        // Arrive at frame.
                        var framePosition = (float)this._frameArray[this._frameOffset] / (float)this._frameRate;
                        if (this.currentPlayTimes == prevPlayTimes)
                        { 
                            // Start.
                            if (prevTime <= framePosition)
                            {
                                this._OnCrossFrame(this._frameIndex);
                            }
                        }
                        else if (this._position <= framePosition)
                        { 
                            // Loop complete.
                            if (!isReverse && loopCompleteEvent != null)
                            { 
                                // Add loop complete event before first frame.
                                this._armature._dragonBones.BufferEvent(loopCompleteEvent);
                                loopCompleteEvent = null;
                            }

                            this._OnCrossFrame(this._frameIndex);
                        }
                    }
                }

                if (loopCompleteEvent != null)
                {
                    this._armature._dragonBones.BufferEvent(loopCompleteEvent);
                }

                if (completeEvent != null)
                {
                    this._armature._dragonBones.BufferEvent(completeEvent);
                }
            }
        }

        public void SetCurrentTime(float value)
        {
            this._SetCurrentTime(value);
            this._frameIndex = -1;
        }
    }
    /**
     * @internal
     * @private
     */
    internal class ZOrderTimelineState : TimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            if (this.playState >= 0)
            {
                var count = this._frameArray[this._frameOffset + 1];
                if (count > 0)
                {
                    this._armature._SortZOrder(this._frameArray, (int)this._frameOffset + 2);
                }
                else
                {
                    this._armature._SortZOrder(null, 0);
                }
            }
        }

        protected override void _OnUpdateFrame() { }
    }
    /**
     * @internal
     * @private
     */
    internal class BoneAllTimelineState : BoneTimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            if (this._timelineData != null)
            {
                var valueOffset = (int)this._animationData.frameFloatOffset + this._frameValueOffset + this._frameIndex * 6; // ...(timeline value offset)|xxxxxx|xxxxxx|(Value offset)xxxxx|(Next offset)xxxxx|xxxxxx|xxxxxx|...
                var scale = this._armature._armatureData.scale;
                var frameFloatArray = this._dragonBonesData.frameFloatArray;
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;

                current.x = frameFloatArray[valueOffset++] * scale;
                current.y = frameFloatArray[valueOffset++] * scale;
                current.rotation = frameFloatArray[valueOffset++];
                current.skew = frameFloatArray[valueOffset++];
                current.scaleX = frameFloatArray[valueOffset++];
                current.scaleY = frameFloatArray[valueOffset++];

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        valueOffset = (int)this._animationData.frameFloatOffset + this._frameValueOffset;
                    }

                    delta.x = frameFloatArray[valueOffset++] * scale - current.x;
                    delta.y = frameFloatArray[valueOffset++] * scale - current.y;
                    delta.rotation = frameFloatArray[valueOffset++] - current.rotation;
                    delta.skew = frameFloatArray[valueOffset++] - current.skew;
                    delta.scaleX = frameFloatArray[valueOffset++] - current.scaleX;
                    delta.scaleY = frameFloatArray[valueOffset++] - current.scaleY;
                }
                else
                {
                    delta.x = 0.0f;
                    delta.y = 0.0f;
                    delta.rotation = 0.0f;
                    delta.skew = 0.0f;
                    delta.scaleX = 0.0f;
                    delta.scaleY = 0.0f;
                }
            }
            else
            {
                // Pose.
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;
                current.x = 0.0f;
                current.y = 0.0f;
                current.rotation = 0.0f;
                current.skew = 0.0f;
                current.scaleX = 1.0f;
                current.scaleY = 1.0f;
                delta.x = 0.0f;
                delta.y = 0.0f;
                delta.rotation = 0.0f;
                delta.skew = 0.0f;
                delta.scaleX = 0.0f;
                delta.scaleY = 0.0f;
            }
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            var current = this.bonePose.current;
            var delta = this.bonePose.delta;
            var result = this.bonePose.result;

            this.bone._transformDirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }
            
            result.x = current.x + delta.x * this._tweenProgress;
            result.y = current.y + delta.y * this._tweenProgress;
            result.rotation = current.rotation + delta.rotation * this._tweenProgress;
            result.skew = current.skew + delta.skew * this._tweenProgress;
            result.scaleX = current.scaleX + delta.scaleX * this._tweenProgress;
            result.scaleY = current.scaleY + delta.scaleY * this._tweenProgress;
        }

        public override void FadeOut()
        {
            var result = this.bonePose.result;
            result.rotation = Transform.NormalizeRadian(result.rotation);
            result.skew = Transform.NormalizeRadian(result.skew);
        }
    }
    /**
     * @internal
     * @private
     */
    internal class BoneTranslateTimelineState : BoneTimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            if (this._timelineData != null)
            {
                var valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset + this._frameIndex * 2;
                var scale = this._armature._armatureData.scale;
                var frameFloatArray = this._dragonBonesData.frameFloatArray;
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;

                current.x = frameFloatArray[valueOffset++] * scale;
                current.y = frameFloatArray[valueOffset++] * scale;

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset;
                    }

                    delta.x = frameFloatArray[valueOffset++] * scale - current.x;
                    delta.y = frameFloatArray[valueOffset++] * scale - current.y;
                }
                else
                {
                    delta.x = 0.0f;
                    delta.y = 0.0f;
                }
            }
            else
            {
                // Pose.
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;
                current.x = 0.0f;
                current.y = 0.0f;
                delta.x = 0.0f;
                delta.y = 0.0f;
            }
        }
        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            var current = this.bonePose.current;
            var delta = this.bonePose.delta;
            var result = this.bonePose.result;

            this.bone._transformDirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }
            
            result.x = current.x + delta.x * this._tweenProgress;
            result.y = current.y + delta.y * this._tweenProgress;
        }
    }
    /**
     * @internal
     * @private
     */
    internal class BoneRotateTimelineState : BoneTimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            if (this._timelineData != null)
            {
                var valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset + this._frameIndex * 2;
                var frameFloatArray = this._dragonBonesData.frameFloatArray;
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;

                current.rotation = frameFloatArray[valueOffset++];
                current.skew = frameFloatArray[valueOffset++];

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset;
                    }

                    delta.rotation = frameFloatArray[valueOffset++] - current.rotation;
                    delta.skew = frameFloatArray[valueOffset++] - current.skew;
                }
                else
                {
                    delta.rotation = 0.0f;
                    delta.skew = 0.0f;
                }
            }
            else
            {
                // Pose.
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;
                current.rotation = 0.0f;
                current.skew = 0.0f;
                delta.rotation = 0.0f;
                delta.skew = 0.0f;
            }
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            var current = this.bonePose.current;
            var delta = this.bonePose.delta;
            var result = this.bonePose.result;

            this.bone._transformDirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }

            result.rotation = current.rotation + delta.rotation * this._tweenProgress;
            result.skew = current.skew + delta.skew * this._tweenProgress;
        }

        public override void FadeOut()
        {
            var result = this.bonePose.result;
            result.rotation = Transform.NormalizeRadian(result.rotation);
            result.skew = Transform.NormalizeRadian(result.skew);
        }
    }
    /**
     * @internal
     * @private
     */
    internal class BoneScaleTimelineState : BoneTimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            if (this._timelineData != null)
            {
                var valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset + this._frameIndex * 2;
                var frameFloatArray = this._dragonBonesData.frameFloatArray;
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;

                current.scaleX = frameFloatArray[valueOffset++];
                current.scaleY = frameFloatArray[valueOffset++];

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset;
                    }

                    delta.scaleX = frameFloatArray[valueOffset++] - current.scaleX;
                    delta.scaleY = frameFloatArray[valueOffset++] - current.scaleY;
                }
                else
                {
                    delta.scaleX = 0.0f;
                    delta.scaleY = 0.0f;
                }
            }
            else
            {
                // Pose.
                var current = this.bonePose.current;
                var delta = this.bonePose.delta;
                current.scaleX = 1.0f;
                current.scaleY = 1.0f;
                delta.scaleX = 0.0f;
                delta.scaleY = 0.0f;
            }
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            var current = this.bonePose.current;
            var delta = this.bonePose.delta;
            var result = this.bonePose.result;

            this.bone._transformDirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }

            result.scaleX = current.scaleX + delta.scaleX * this._tweenProgress;
            result.scaleY = current.scaleY + delta.scaleY * this._tweenProgress;
        }
    }
    /**
     * @internal
     * @private
     */
    internal class SlotDislayTimelineState : SlotTimelineState
    {
        protected override void _OnArriveAtFrame()
        {
            if (this.playState >= 0)
            {
                var displayIndex = this._timelineData != null ? this._frameArray[this._frameOffset + 1] : this.slot.slotData.displayIndex;
                if (this.slot.displayIndex != displayIndex)
                {
                   this.slot._SetDisplayIndex(displayIndex, true);
                }
            }
        }
    }
    /**
     * @internal
     * @private
     */
    internal class SlotColorTimelineState : SlotTimelineState
    {
        private bool _dirty;
        private readonly int[] _current = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        private readonly int[] _delta = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        private readonly float[] _result = new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };

        protected override void _OnClear()
        {
            base._OnClear();

            this._dirty = false;
        }

        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            if (this._timelineData != null)
            {
                var intArray = this._dragonBonesData.intArray;
                var frameIntArray = this._dragonBonesData.frameIntArray;
                var valueOffset = this._animationData.frameIntOffset + this._frameValueOffset + this._frameIndex * 1; // ...(timeline value offset)|x|x|(Value offset)|(Next offset)|x|x|...
                int colorOffset = frameIntArray[valueOffset];

                if (colorOffset < 0)
                {
                    colorOffset += short.MaxValue;
                }
                this._current[0] = intArray[colorOffset++];
                this._current[1] = intArray[colorOffset++];
                this._current[2] = intArray[colorOffset++];
                this._current[3] = intArray[colorOffset++];
                this._current[4] = intArray[colorOffset++];
                this._current[5] = intArray[colorOffset++];
                this._current[6] = intArray[colorOffset++];
                this._current[7] = intArray[colorOffset++];

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        colorOffset = frameIntArray[this._animationData.frameIntOffset + this._frameValueOffset];
                    }
                    else
                    {
                        colorOffset = frameIntArray[valueOffset + 1 * 1];
                    }

                    if (colorOffset < 0)
                    {
                        colorOffset += short.MaxValue;
                    }

                    this._delta[0] = intArray[colorOffset++] - this._current[0];
                    this._delta[1] = intArray[colorOffset++] - this._current[1];
                    this._delta[2] = intArray[colorOffset++] - this._current[2];
                    this._delta[3] = intArray[colorOffset++] - this._current[3];
                    this._delta[4] = intArray[colorOffset++] - this._current[4];
                    this._delta[5] = intArray[colorOffset++] - this._current[5];
                    this._delta[6] = intArray[colorOffset++] - this._current[6];
                    this._delta[7] = intArray[colorOffset++] - this._current[7];
                }
            }
            else
            {
                // Pose.
                var color = this.slot._slotData.color;
                this._current[0] = (int)(color.alphaMultiplier * 100.0f);
                this._current[1] = (int)(color.redMultiplier * 100.0f);
                this._current[2] = (int)(color.greenMultiplier * 100.0f);
                this._current[3] = (int)(color.blueMultiplier * 100.0f);
                this._current[4] = color.alphaOffset;
                this._current[5] = color.redOffset;
                this._current[6] = color.greenOffset;
                this._current[7] = color.blueOffset;
            }
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            this._dirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }

            this._result[0] = (this._current[0] + this._delta[0] * this._tweenProgress) * 0.01f;
            this._result[1] = (this._current[1] + this._delta[1] * this._tweenProgress) * 0.01f;
            this._result[2] = (this._current[2] + this._delta[2] * this._tweenProgress) * 0.01f;
            this._result[3] = (this._current[3] + this._delta[3] * this._tweenProgress) * 0.01f;
            this._result[4] = this._current[4] + this._delta[4] * this._tweenProgress;
            this._result[5] = this._current[5] + this._delta[5] * this._tweenProgress;
            this._result[6] = this._current[6] + this._delta[6] * this._tweenProgress;
            this._result[7] = this._current[7] + this._delta[7] * this._tweenProgress;
        }

        public override void FadeOut()
        {
            this._tweenState = TweenState.None;
            this._dirty = false;
        }

        public override void Update(float passedTime)
        {
            base.Update(passedTime);

            // Fade animation.
            if (this._tweenState != TweenState.None || this._dirty)
            {
                var result = this.slot._colorTransform;

                if (this._animationState._fadeState != 0 || this._animationState._subFadeState != 0)
                {
                    if (result.alphaMultiplier != this._result[0] ||
                        result.redMultiplier != this._result[1] ||
                        result.greenMultiplier != this._result[2] ||
                        result.blueMultiplier != this._result[3] ||
                        result.alphaOffset != this._result[4] ||
                        result.redOffset != this._result[5] ||
                        result.greenOffset != this._result[6] ||
                        result.blueOffset != this._result[7])
                    {
                        var fadeProgress = (float)Math.Pow(this._animationState._fadeProgress, 4);

                        result.alphaMultiplier += (this._result[0] - result.alphaMultiplier) * fadeProgress;
                        result.redMultiplier += (this._result[1] - result.redMultiplier) * fadeProgress;
                        result.greenMultiplier += (this._result[2] - result.greenMultiplier) * fadeProgress;
                        result.blueMultiplier += (this._result[3] - result.blueMultiplier) * fadeProgress;
                        result.alphaOffset += (int)((this._result[4] - result.alphaOffset) * fadeProgress);
                        result.redOffset += (int)((this._result[5] - result.redOffset) * fadeProgress);
                        result.greenOffset += (int)((this._result[6] - result.greenOffset) * fadeProgress);
                        result.blueOffset += (int)((this._result[7] - result.blueOffset) * fadeProgress);

                        this.slot._colorDirty = true;
                    }
                }
                else if (this._dirty)
                {
                    this._dirty = false;
                    if (result.alphaMultiplier != this._result[0] ||
                        result.redMultiplier != this._result[1] ||
                        result.greenMultiplier != this._result[2] ||
                        result.blueMultiplier != this._result[3] ||
                        result.alphaOffset != (int)this._result[4] ||
                        result.redOffset != (int)this._result[5] ||
                        result.greenOffset != (int)this._result[6] ||
                        result.blueOffset != (int)this._result[7])
                    {
                        result.alphaMultiplier = this._result[0];
                        result.redMultiplier = this._result[1];
                        result.greenMultiplier = this._result[2];
                        result.blueMultiplier = this._result[3];
                        result.alphaOffset = (int)this._result[4];
                        result.redOffset = (int)this._result[5];
                        result.greenOffset = (int)this._result[6];
                        result.blueOffset = (int)this._result[7];

                        this.slot._colorDirty = true;
                    }
                }
            }
        }
    }
    /**
     * @internal
     * @private
     */
    internal class SlotFFDTimelineState : SlotTimelineState
    {
        public uint meshOffset;

        private bool _dirty;
        private int _frameFloatOffset;
        private int _valueCount;
        private int _ffdCount;
        private int _valueOffset;
        private readonly List<float> _current = new List<float>();
        private readonly List<float> _delta = new List<float>();
        private readonly List<float> _result = new List<float>();

        protected override void _OnClear()
        {
            base._OnClear();

            this.meshOffset = 0;

            this._dirty = false;
            this._frameFloatOffset = 0;
            this._valueCount = 0;
            this._ffdCount = 0;
            this._valueOffset = 0;
            this._current.Clear();
            this._delta.Clear();
            this._result.Clear();
        }

        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();
            if (this._timelineData != null)
            {
                var valueOffset = this._animationData.frameFloatOffset + this._frameValueOffset + this._frameIndex * this._valueCount;
                var scale = this._armature._armatureData.scale;
                var frameFloatArray = this._dragonBonesData.frameFloatArray;

                if (this._tweenState == TweenState.Always)
                {
                    var nextValueOffset = valueOffset + this._valueCount;
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        nextValueOffset = this._animationData.frameFloatOffset + this._frameValueOffset;
                    }

                    for (var i = 0; i < this._valueCount; ++i)
                    {
                        this._delta[i] = frameFloatArray[nextValueOffset + i] * scale - (this._current[i] = frameFloatArray[valueOffset + i] * scale);
                    }
                }
                else
                {
                    for (var i = 0; i < this._valueCount; ++i)
                    {
                        this._current[i] = frameFloatArray[valueOffset + i] * scale;
                    }
                }
            }
            else
            {
                for (var i = 0; i < this._valueCount; ++i)
                {
                    this._current[i] = 0.0f;
                }
            }
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            this._dirty = true;
            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }

            for (var i = 0; i < this._valueCount; ++i)
            {
                this._result[i] = this._current[i] + this._delta[i] * this._tweenProgress;
            }
        }

        public override void Init(Armature armature, AnimationState animationState, TimelineData timelineData)
        {
            base.Init(armature, animationState, timelineData);

            if (this._timelineData != null)
            {
                var frameIntOffset = this._animationData.frameIntOffset + this._timelineArray[this._timelineData.offset + (int)BinaryOffset.TimelineFrameValueCount];
                this.meshOffset = (uint)this._frameIntArray[frameIntOffset + (int)BinaryOffset.FFDTimelineMeshOffset];
                this._ffdCount = this._frameIntArray[frameIntOffset + (int)BinaryOffset.FFDTimelineFFDCount];
                this._valueCount = this._frameIntArray[frameIntOffset + (int)BinaryOffset.FFDTimelineValueCount];
                this._valueOffset = this._frameIntArray[frameIntOffset + (int)BinaryOffset.FFDTimelineValueOffset];
                this._frameFloatOffset = this._frameIntArray[frameIntOffset + (int)BinaryOffset.FFDTimelineFloatOffset] + (int)this._animationData.frameFloatOffset;
            }
            else
            {
                this._valueCount = 0;
            }

            this._current.ResizeList(this._valueCount);
            this._delta.ResizeList(this._valueCount);
            this._result.ResizeList(this._valueCount);

            for (var i = 0; i < this._valueCount; ++i)
            {
                this._delta[i] = 0.0f;
            }
        }

        public override void FadeOut()
        {
            this._tweenState = TweenState.None;
            this._dirty = false;
        }

        public override void Update(float passedTime)
        {
            if (this.slot._meshData == null || this.slot._meshData.offset != this.meshOffset)
            {
                return;
            }

            base.Update(passedTime);

            // Fade animation.
            if (this._tweenState != TweenState.None || this._dirty)
            {
                var result = this.slot._ffdVertices;
                if (this._timelineData != null)
                {
                    if (this._animationState._fadeState != 0 || this._animationState._subFadeState != 0)
                    {
                        var fadeProgress = (float)Math.Pow(this._animationState._fadeProgress, 2);

                        for (var i = 0; i < this._ffdCount; ++i)
                        {
                            if (i < this._valueOffset)
                            {
                                result[i] += (this._frameFloatArray[this._frameFloatOffset + i] - result[i]) * fadeProgress;
                            }
                            else if (i < this._valueOffset + this._valueCount)
                            {
                                result[i] += (this._result[i - this._valueOffset] - result[i]) * fadeProgress;
                            }
                            else
                            {
                                result[i] += (this._frameFloatArray[this._frameFloatOffset + i - this._valueCount] - result[i]) * fadeProgress;
                            }
                        }

                        this.slot._meshDirty = true;
                    }
                    else if (this._dirty)
                    {
                        this._dirty = false;

                        for (var i = 0; i < this._ffdCount; ++i)
                        {
                            if (i < this._valueOffset)
                            {
                                result[i] = this._frameFloatArray[this._frameFloatOffset + i];
                            }
                            else if (i < this._valueOffset + this._valueCount)
                            {
                                result[i] = this._result[i - this._valueOffset];
                            }
                            else
                            {
                                result[i] = this._frameFloatArray[this._frameFloatOffset + i - this._valueCount];
                            }
                        }

                        this.slot._meshDirty = true;
                    }
                }
                else
                {
                    this._ffdCount = result.Count; //
                    if (this._animationState._fadeState != 0 || this._animationState._subFadeState != 0)
                    {
                        var fadeProgress = (float)Math.Pow(this._animationState._fadeProgress, 2);
                        for (var i = 0; i < this._ffdCount; ++i)
                        {
                            result[i] += (0.0f - result[i]) * fadeProgress;
                        }

                        this.slot._meshDirty = true;
                    }
                    else if (this._dirty)
                    {
                        this._dirty = false;

                        for (var i = 0; i < this._ffdCount; ++i)
                        {
                            result[i] = 0.0f;
                        }

                        this.slot._meshDirty = true;
                    }
                }
                
            }
        }
    }

    internal class IKConstraintTimelineState : ConstraintTimelineState
    {
        private float _current;
        private float _delta;

        protected override void _OnClear()
        {
            base._OnClear();

            this._current = 0.0f;
            this._delta = 0.0f;
        }

        protected override void _OnArriveAtFrame()
        {
            base._OnArriveAtFrame();

            var ikConstraint = this.constraint as IKConstraint;

            if (this._timelineData != null)
            {
                var valueOffset = this._animationData.frameIntOffset + this._frameValueOffset + this._frameIndex * 2;
                var frameIntArray = this._frameIntArray;
                var bendPositive = frameIntArray[valueOffset++] != 0;
                this._current = frameIntArray[valueOffset++] * 0.01f;

                if (this._tweenState == TweenState.Always)
                {
                    if (this._frameIndex == this._frameCount - 1)
                    {
                        valueOffset = this._animationData.frameIntOffset + this._frameValueOffset; // + 0 * 2
                    }

                    this._delta = frameIntArray[valueOffset + 1] * 0.01f - this._current;
                }
                else
                {
                    this._delta = 0.0f;
                }

                ikConstraint._bendPositive = bendPositive;
            }
            else
            {
                var ikConstraintData = ikConstraint._constraintData as IKConstraintData;
                this._current = ikConstraintData.weight;
                this._delta = 0.0f;
                ikConstraint._bendPositive = ikConstraintData.bendPositive;
            }

            ikConstraint.InvalidUpdate();
        }

        protected override void _OnUpdateFrame()
        {
            base._OnUpdateFrame();

            if (this._tweenState != TweenState.Always)
            {
                this._tweenState = TweenState.None;
            }

            var ikConstraint = this.constraint as IKConstraint;
            ikConstraint._weight = this._current + this._delta * this._tweenProgress;
            ikConstraint.InvalidUpdate();
        }
    }
}
