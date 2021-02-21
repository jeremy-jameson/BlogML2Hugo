using System;

namespace BlogML2Hugo.Core
{
    public interface IUrlMapper
    {
        Uri GetMappedUrl(Uri url);

        bool IsMappedUrl(Uri url);
    }
}
