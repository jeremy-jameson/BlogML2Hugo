﻿using BlogML.Xml;

namespace BlogML2Hugo
{
    public interface IPostConversionStep
    {
        void Execute(PostConversionData postConversionData);
    }
}