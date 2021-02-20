using System;

namespace BlogML2Hugo
{
    public interface IUrlMapper
    {
        Uri GetMappedUrl(Uri url);

        bool IsMappedUrl(Uri url);
    }
}
