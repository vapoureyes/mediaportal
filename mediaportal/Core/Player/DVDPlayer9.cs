using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MediaPortal.GUI.Library;
using DirectX.Capture;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Direct3D = Microsoft.DirectX.Direct3D;


using DShowNET;
using DShowNET.Dvd;


namespace MediaPortal.Player
{
  /// <summary>
  /// 
  /// </summary>
  public class DVDPlayer9 : DVDPlayer 
  {
    const uint  VFW_E_DVD_DECNOTENOUGH    =0x8004027B;
    const uint  VFW_E_DVD_RENDERFAIL      =0x8004027A;

    GCHandle                  myHandle;
    AllocatorWrapper.Allocator allocator;
    PlaneScene                 m_scene=null;
    /// <summary> create the used COM components and get the interfaces. </summary>
    protected override bool GetInterfaces(string strPath)
    {
      int		            hr;
      Type	            comtype = null;
      object	          comobj = null;
      m_bFreeNavigator=true;
      dvdInfo=null;
      dvdCtrl=null;

      IBaseFilter VMR9Filter=null;
      string strDVDAudioRenderer="";
      string strDVDNavigator="";
      string strARMode="";
      string strDisplayMode="";
      using(AMS.Profile.Xml   xmlreader=new AMS.Profile.Xml("MediaPortal.xml"))
      {
        strDVDAudioRenderer=xmlreader.GetValueAsString("dvdplayer","audiorenderer","");
        strDVDNavigator=xmlreader.GetValueAsString("dvdplayer","navigator","");
        strARMode=xmlreader.GetValueAsString("dvdplayer","armode","").ToLower();
        if ( strARMode=="crop") arMode=AmAspectRatioMode.AM_ARMODE_CROP;
        if ( strARMode=="letterbox") arMode=AmAspectRatioMode.AM_ARMODE_LETTER_BOX;
        if ( strARMode=="stretch") arMode=AmAspectRatioMode.AM_ARMODE_STRETCHED;
        if ( strARMode=="follow stream") arMode=AmAspectRatioMode.AM_ARMODE_STRETCHED_AS_PRIMARY;

        strDisplayMode=xmlreader.GetValueAsString("dvdplayer","displaymode","").ToLower();
        if (strDisplayMode=="default") m_iVideoPref=0;
        if (strDisplayMode=="16:9") m_iVideoPref=1;
        if (strDisplayMode=="4:3 pan scan") m_iVideoPref=2;
        if (strDisplayMode=="4:3 letterbox") m_iVideoPref=3;
      }
      try 
      {
        
        comtype = Type.GetTypeFromCLSID( Clsid.DvdGraphBuilder );
        if( comtype == null )
          throw new NotSupportedException( "DirectX (8.1 or higher) not installed?" );
        comobj = Activator.CreateInstance( comtype );
        dvdGraph = (IDvdGraphBuilder) comobj; comobj = null;

        hr = dvdGraph.GetFiltergraph( out graphBuilder );
        if( hr != 0 )
          Marshal.ThrowExceptionForHR( hr );

        
        DvdRenderStatus status;
        if (strPath.Length==0) strPath=null;
        hr=dvdGraph.RenderDvdVideoVolume(strPath,DvdGraphFlags.SwDecPrefer|DvdGraphFlags.VMR9Only,out status);
        if( hr != 0 )
        {
          if (((uint)hr)==VFW_E_DVD_DECNOTENOUGH)
            Log.Write("FAILED:Unable to DVD. Missing codecs which support VMR9");
          else if (((uint)hr)==VFW_E_DVD_RENDERFAIL)
            Log.Write("FAILED:Unable to DVD. Some basic error occurred in building the graph");
          else
            Log.Write("FAILED:Unable to render volume:{0} error:0x{1:X}", strPath,hr);
          if (status.vpeStatus!=0) Log.Write("  overlay error :{0:x}", status.vpeStatus);
          if (status.volInvalid) Log.Write("  invalid volume:{0}", strPath);
          if (status.volUnknown) Log.Write("  NO DVD found at :{0}", strPath);
          if (status.noLine21In) Log.Write("  The video decoder doesn't produce line21 data");
          if (status.noLine21Out) Log.Write("  the video decoder can't be shown as closed captioning on video due to a problem with graph building");
          if (status.numStreamsFailed>0) Log.Write("  streams failed:{0} of {1}",status.numStreamsFailed,status.numStreams);
          if ((status.failedStreams & DvdStreamFlags.Audio)!=0) Log.Write("  audio stream failed");
          if ((status.failedStreams & DvdStreamFlags.Video)!=0) Log.Write("  video stream failed");
          if ((status.failedStreams & DvdStreamFlags.SubPic)!=0) Log.Write("  subpic stream failed");
          //  Marshal.ThrowExceptionForHR( hr );
        }

        DsROT.AddGraphToRot( graphBuilder, out rotCookie );		// graphBuilder capGraph
        AddVMR9(dvdGraph);

        try
        {
            hr = SetAllocPresenter(VMR9Filter, GUIGraphicsContext.form as Control);
            if (hr!=0) 
            {
              Log.Write("VideoPlayer9:Failed to set VMR9 allocator/presentor");
              return false;
            }

            m_bFreeNavigator=false;
            //Marshal.ReleaseComObject( dvdbasefilter); dvdbasefilter = null;              
        }
        catch(Exception ex)
        {
          string strEx=ex.Message;
        }
        Guid riid ;

			
        if (dvdInfo==null)
        {
          riid = typeof( IDvdInfo2 ).GUID;
          hr = dvdGraph.GetDvdInterface( ref riid, out comobj );
          if( hr < 0 )
            Marshal.ThrowExceptionForHR( hr );
          dvdInfo = (IDvdInfo2) comobj; comobj = null;
        }

        if (dvdCtrl==null)
        {
          riid = typeof( IDvdControl2 ).GUID;
          hr = dvdGraph.GetDvdInterface( ref riid, out comobj );
          if( hr < 0 )
            Marshal.ThrowExceptionForHR( hr );
          dvdCtrl = (IDvdControl2) comobj; comobj = null;
        }


        mediaCtrl	= (IMediaControl)  graphBuilder;
        mediaEvt	= (IMediaEventEx)  graphBuilder;
        basicAudio	= graphBuilder as IBasicAudio;
        mediaPos	= (IMediaPosition) graphBuilder;
        basicVideo	= graphBuilder as IBasicVideo2;

      

        // disable Closed Captions!
        IBaseFilter basefilter;
        graphBuilder.FindFilterByName("Line 21 Decoder", out basefilter);
        if (basefilter==null)
          graphBuilder.FindFilterByName("Line21 Decoder", out basefilter);
        if (basefilter!=null)
        {
          line21Decoder=(IAMLine21Decoder)basefilter;
          if (line21Decoder!=null)
          {
            AMLine21CCState state=AMLine21CCState.Off;
            hr=line21Decoder.SetServiceState(ref state);
            if (hr==0)
            {
              Log.Write("DVDPlayer:Closed Captions disabled");
            }
            else
            {
              Log.Write("DVDPlayer:failed 2 disable Closed Captions");
            }
          }
        }

        DirectShowUtil.SetARMode(graphBuilder,arMode);
        DirectShowUtil.EnableDeInterlace(graphBuilder);
        //m_ovMgr = new OVTOOLLib.OvMgrClass();
        //m_ovMgr.SetGraph(graphBuilder);



        m_iVideoWidth=allocator.NativeSize.Width;
        m_iVideoHeight=allocator.NativeSize.Height;

        if( VMR9Filter != null )
          Marshal.ReleaseComObject( VMR9Filter ); VMR9Filter = null;

        m_bStarted=true;
        return true;
      }
      catch( Exception )
      {
        //MessageBox.Show( this, "Could not get interfaces\r\n" + ee.Message, "DVDPlayer.NET", MessageBoxButtons.OK, MessageBoxIcon.Stop );
        CloseInterfaces();
        return false;
      }
      finally
      {
        if( comobj != null )
          Marshal.ReleaseComObject( comobj ); comobj = null;
      }
    }

    void AddVMR9(IDvdGraphBuilder dvdBuilder)
    {
      //IVideoMixingRenderer9

      //IVMRFilterConfig9
      IVMRFilterConfig9 FilterConfig9 = dvdBuilder as IVMRFilterConfig9;
      if (FilterConfig9==null) 
      {
        Log.Write("VideoPlayer9:Failed to get IVMRFilterConfig9 ");
        return;
      }
      int hr = FilterConfig9.SetRenderingMode(VMR9.VMRMode_Renderless);
      if (hr!=0) 
      {
        Log.Write("VideoPlayer9:Failed to set VMR9 to renderless mode");
        return;
      }

      // needed to put VMR9 in mixing mode instead of pass-through mode
        
      hr = FilterConfig9.SetNumberOfStreams(1);
      if (hr!=0) 
      {
        Log.Write("VideoPlayer9:Failed to set VMR9 streams to 1");
        return;
      }
    }
    
    /// <summary> do cleanup and release DirectShow. </summary>
    protected override void CloseInterfaces()
    {
      int hr;
      try 
      {
        Log.Write("DVDPlayer:cleanup DShow graph");
        if( dvdCtrl != null )
          hr = dvdCtrl.SetOption( DvdOptionFlag.ResetOnStop, true );

        if( mediaCtrl != null )
        {
          hr = mediaCtrl.Stop();
          mediaCtrl = null;
        }
        m_state = PlayState.Stopped;

        if( mediaEvt != null )
        {
          hr = mediaEvt.SetNotifyWindow( IntPtr.Zero, WM_DVD_EVENT, IntPtr.Zero );
          mediaEvt = null;
        }

        if (allocator!=null)
        {
          allocator.UnAdviseNotify();
        }
        if (myHandle.IsAllocated)
        {
          myHandle.Free();
        }
        allocator=null;
          
        if (m_scene!=null)
        {
          m_scene.Stop();
          m_scene.Deinit();
          m_scene=null;
        }



        if( audioRenderer != null )
          Marshal.ReleaseComObject( audioRenderer); audioRenderer = null;

          m_bVisible=false;
    		
        if( cmdOption.dvdCmd != null )
          Marshal.ReleaseComObject( cmdOption.dvdCmd ); cmdOption.dvdCmd = null;
        pendingCmd = false;


        if (rotCookie !=0) DsROT.RemoveGraphFromRot( ref rotCookie );		// graphBuilder capGraph
        if( graphBuilder != null )
        {
          DsUtils.RemoveFilters(graphBuilder);
          Marshal.ReleaseComObject( graphBuilder ); graphBuilder = null;
        }
        if (m_bFreeNavigator)
        {
          if( dvdCtrl != null )
            Marshal.ReleaseComObject( dvdCtrl ); 
        }
        dvdCtrl = null;

        if (m_bFreeNavigator)
        {
          if( dvdInfo != null )
            Marshal.ReleaseComObject( dvdInfo ); 
        }
        dvdInfo = null;

        if( dvdGraph != null )
          Marshal.ReleaseComObject( dvdGraph ); dvdGraph = null;

        line21Decoder=null;
        dvdInfo=null;
        basicVideo=null;
        basicAudio=null;
        mediaPos=null;
        m_state = PlayState.Init;
        GUIGraphicsContext.form.Invalidate(true);          
        GUIGraphicsContext.form.Activate();
      }
      catch( Exception ex)
      {
        Log.Write("DVDPlayer:exception while cleanuping DShow graph {0} {1}",ex.Message, ex.StackTrace);
      }
    }

    int SetAllocPresenter(IBaseFilter filter, Control control)
    {
      IVMRSurfaceAllocatorNotify9 lpIVMRSurfAllocNotify = filter as IVMRSurfaceAllocatorNotify9;

      if (lpIVMRSurfAllocNotify == null)
      {
        Log.Write("VideoPlayer9:Failed to get IVMRSurfaceAllocatorNotify9");
        return -1;
      }
      m_scene= new PlaneScene(m_renderFrame);
      allocator = new AllocatorWrapper.Allocator(control, m_scene);
      //allocator.SetTextureSize( new Size(720,576) );
      IntPtr hMonitor;
      AdapterInformation ai = Manager.Adapters.Default;

      hMonitor = Manager.GetAdapterMonitor(ai.Adapter);
      IntPtr upDevice = DsUtils.GetUnmanagedDevice(allocator.Device);
				 
      int hr = lpIVMRSurfAllocNotify.SetD3DDevice(upDevice, hMonitor);
      //Marshal.AddRef(upDevice);
      if (hr != 0)
      {
        Log.Write("VideoPlayer9:Failed to get SetD3DDevice()");
        return hr;
      }
      // this must be global. If it gets garbage collected, pinning won't exist...
      myHandle = GCHandle.Alloc(allocator, GCHandleType.Pinned);
      hr = allocator.AdviseNotify(lpIVMRSurfAllocNotify);
      if (hr != 0)
      {
        Log.Write("VideoPlayer9:Failed to AdviseNotify()");
        return hr;
      }
      hr = lpIVMRSurfAllocNotify.AdviseSurfaceAllocator(0xACDCACDC, allocator);
      if (hr !=0)
      {
        Log.Write("VideoPlayer9:Failed to AdviseSurfaceAllocator()");
      }
      return hr;
    }

    protected override void OnProcess()
    {
      if (Paused || menuMode!=MenuMode.No )
      {
        //repaint
        allocator.Repaint();
      }
      m_SourceRect=m_scene.SourceRect;
      m_VideoRect=m_scene.DestRect;
    }

    public override bool DoesOwnRendering
    {
      get { return true;}
    }      
  }
}
