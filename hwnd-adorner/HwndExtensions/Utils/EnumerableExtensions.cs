using System;
using System.Collections;
using System.Collections.Generic;

namespace HwndExtensions.Utils
{
    public static class EnumerableExtensions
    {
        private static IEnumerable<int> ReverseRange(int start, int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException();

            for (int i = start + count - 1; i >= start; --i)
                yield return i;
        }
    }

    public class EmptyEnumerator : IEnumerator
    {
        private static IEnumerator m_instance;

        public static IEnumerator Instance
        {
            get
            {
                if (m_instance == null)
                    m_instance = (IEnumerator)new EmptyEnumerator();
                return m_instance;
            }
        }

        public object Current
        {
            get
            {
                throw new InvalidOperationException();
            }
        }

        private EmptyEnumerator()
        {
        }

        public void Reset()
        {
        }

        public bool MoveNext()
        {
            return false;
        }
    }

    public class SingleChildEnumerator : IEnumerator
    {
        private enum State
        {
            Reset, Current, Finished
        }

        private readonly object m_child;
        private State m_state;

        public SingleChildEnumerator(object child)
        {
            m_child = child;
        }

        public object Current
        {
            get
            {
                if (m_state == State.Current)
                    return m_child;

                throw new InvalidOperationException();
            }
        }

        public void Reset()
        {
            m_state = State.Reset;
        }

        public bool MoveNext()
        {
            switch (m_state)
            {
                case State.Reset:
                    m_state = State.Current;
                    return true;

                case State.Current:
                    m_state = State.Finished;
                    return false;

                case State.Finished:
                    return false;

                default:
                    return false;
            }
        }
    }
}
