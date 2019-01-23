using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace TraderTools.Basics
{
    public class Market
    {
        [NonSerialized]
        private Subject<Market> _updatedObservable;

        private string _comments;

        public string Name { get; set; }

        public string Comments
        {
            get => _comments;
            set
            {
                _comments = value;
                UpdatedSubject.OnNext(this);
            }
        }

        private Subject<Market> UpdatedSubject => _updatedObservable ?? (_updatedObservable = new Subject<Market>());

        public IObservable<Market> UpdatedObservable => UpdatedSubject.AsObservable();
    }
}