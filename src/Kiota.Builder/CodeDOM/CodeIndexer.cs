﻿namespace Kiota.Builder
{
    public class CodeIndexer : CodeTerminal
    {
        public CodeIndexer(CodeElement parent): base(parent)
        {
            
        }
        public CodeType IndexType;
        public CodeType ReturnType;
    }
}