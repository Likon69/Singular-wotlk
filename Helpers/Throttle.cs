using System;
using System.Collections.Generic;
using TreeSharp;

namespace Singular.Helpers
{
    /// <summary>
    /// Limits the number of times the child returns RunStatus.Success within a given time span.
    /// Returns Failure (or the configured limitStatus) when the limit is reached; otherwise
    /// passes through the child's result unchanged.
    /// Ported from Singular 6.X.X Helpers/Throttle.cs — adapted for TreeSharp namespace.
    /// </summary>
    public class Throttle : Decorator
    {
        private DateTime _end;
        private int _count;
        private readonly RunStatus _limitStatus;

        public TimeSpan TimeFrame { get; set; }
        public int Limit { get; set; }

        private static Composite ChildComposite(params Composite[] children)
        {
            if (children.Length == 1)
                return children[0];
            return new PrioritySelector(children);
        }

        public Throttle(int limit, TimeSpan timeFrame, RunStatus limitStatus, params Composite[] children)
            : base(ChildComposite(children))
        {
            TimeFrame = timeFrame;
            Limit = limit;
            _end = DateTime.MinValue;
            _count = 0;
            _limitStatus = limitStatus;
        }

        public Throttle(int limit, TimeSpan timeFrame, params Composite[] children)
            : this(limit, timeFrame, RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <param name="limit">Max successes allowed.</param>
        /// <param name="timeSeconds">Time window in seconds.</param>
        public Throttle(int limit, int timeSeconds, params Composite[] children)
            : this(limit, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <summary>Allows 1 success per <paramref name="timeSeconds"/> seconds.</summary>
        public Throttle(int timeSeconds, params Composite[] children)
            : this(1, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {
        }

        public Throttle(TimeSpan timeFrame, params Composite[] children)
            : this(1, timeFrame, RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <summary>Default: 1 success per 250ms.</summary>
        public Throttle(params Composite[] children)
            : this(1, TimeSpan.FromMilliseconds(250), RunStatus.Failure, ChildComposite(children))
        {
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (DateTime.Now < _end && _count >= Limit)
            {
                yield return _limitStatus;
                yield break;
            }

            if (DecoratedChild == null)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            DecoratedChild.Start(context);

            while (DecoratedChild.Tick(context) == RunStatus.Running)
                yield return RunStatus.Running;

            DecoratedChild.Stop(context);

            if (DecoratedChild.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            if (DateTime.Now > _end)
            {
                _count = 0;
                _end = DateTime.Now + TimeFrame;
            }

            _count++;

            yield return RunStatus.Success;
        }
    }
}
