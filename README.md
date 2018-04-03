# ws-proxy

ws-proxy is a client and server toolset to access regular TCP services through a WebSocket connection.

This is useful in situations where ports are blocked, such as free Wi-Fi hotspots, passenger aircraft, etc.

This is a little project I whipped up in a few hours. Don't expect support.

## Usage

The server (ws-proxy-server) has to be configured and installed ahead of time.

The server exposes a single pre-configured TCP service. This is configured through the following environment variables:

- `WsProxy:TargetHost` is the host where the TCP service lives.
- `WsProxy:TargetPort` is the port number of the TCP service.

Authentication is performed via a shared secret / password. This is also configured through an environment variable:

- `WsProxy:Password` is the plain-text password. Any trailing whitespace is ignored.

If your system does not allow environment variables that contain a colon, you can use double-underscores e.g.: `WsProxy__TargetHost`, `WsProxy__TargetPort`, `WsProxy__Password`.

A docker container is available at [yaakovh/ws-proxy-server](https://hub.docker.com/r/yaakovh/ws-proxy-server/).

You can use this as a container in a Kubernetes pod to expose a single service over HTTP - for example, to access a private HTTP proxy, you can use the following deployment:

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  creationTimestamp: null
  name: ws-proxy
spec:
  replicas: 1
  template:
    metadata:
      labels:
        service: ws-proxy
    spec:
      containers:
        - name: proxy
          image: yaakovh/ws-proxy-server:latest
          env:
            - name: WsProxy__TargetHost
              value: "127.0.0.1"
            - name: WsProxy__TargetPort
              value: "3128"
            - name: WsProxy__Password
              valueFrom:
                secretKeyRef:
                  name: ws-proxy-password
                  key: password
          ports:
            - containerPort: 80
              hostPort: 80
              protocol: TCP
        - name: squid
          image: sameersbn/squid:3.3.8-23
          ports:
            - containerPort: 3128
              hostPort: 3128
              protocol: TCP
```

Creating a corresponding Secret, Service and Ingress are left as an excercise for the reader.
