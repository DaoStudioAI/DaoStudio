using System;
using System.Collections.Generic;
using System.Text;

namespace DaoStudio.Common
{
    /// <summary>
    /// Exception class that needs to be handled by Users
    /// </summary>
    public class UIException: Exception
    {

        public UIException(string msg):base(msg)
        {
        }


   

    }
}
