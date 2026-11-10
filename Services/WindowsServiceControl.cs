using System;
using System.ServiceProcess;

namespace TaggJack.Services;

public static class WindowsServiceControl
{
	public static bool IsServiceInstalled(string serviceName)
	{
		try
		{
			using ServiceController serviceController = new ServiceController(serviceName);
			_ = serviceController.Status;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static ServiceControllerStatus? GetStatus(string serviceName)
	{
		try
		{
			using ServiceController serviceController = new ServiceController(serviceName);
			return serviceController.Status;
		}
		catch
		{
			return null;
		}
	}

	public static void StartService(string serviceName, TimeSpan timeout)
	{
		using ServiceController serviceController = new ServiceController(serviceName);
		if (serviceController.Status == ServiceControllerStatus.Running || serviceController.Status == ServiceControllerStatus.StartPending)
		{
			serviceController.WaitForStatus(ServiceControllerStatus.Running, timeout);
			return;
		}
		serviceController.Start();
		serviceController.WaitForStatus(ServiceControllerStatus.Running, timeout);
	}

	public static void StopService(string serviceName, TimeSpan timeout)
	{
		using ServiceController serviceController = new ServiceController(serviceName);
		if (serviceController.Status == ServiceControllerStatus.Stopped || serviceController.Status == ServiceControllerStatus.StopPending)
		{
			serviceController.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
			return;
		}
		serviceController.Stop();
		serviceController.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
	}
}
