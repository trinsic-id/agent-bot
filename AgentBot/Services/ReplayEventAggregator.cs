using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentFramework.Core.Contracts;

namespace AgentBot.Services
{
    public class ReplayEventAggregator : IEventAggregator
    {
        private readonly ReplaySubject<object> _subject;

        public ReplayEventAggregator(TimeSpan replayWindow)
        {
            _subject = new ReplaySubject<object>(replayWindow);
        }

        /// <inheritdoc />
        public IObservable<TEvent> GetEventByType<TEvent>() => _subject.OfType<TEvent>().AsObservable();

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent eventToPublish) => _subject.OnNext(eventToPublish);
    }
}
