using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Medallion
{
    internal sealed class Trampoline
    {
        [ThreadStatic]
        private static Trampoline? current;

        public static Trampoline Current => current ??= new Trampoline();

        private readonly Stack<Action> _stack = new Stack<Action>();
        private FastRandom random = FastRandom.Create(); // todo revisit

        public bool ShouldTrampoline => this.random.NextShort(0b11111) == 0 || true; // todo

        public void PushAction(Action action) => this._stack.Push(action);

        public void RunTrampoline()
        {
            var stack = this._stack;
            while (stack.Count != 0)
            {
                stack.Pop()();
            }
        }
    }
}
