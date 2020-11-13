# Rolling a Slow App

This is a demonstrator app (build with ASP.NET) and Kubernetes manifests to deploy it, along with an nginx-ingress controller. The idea is to test what happens to pods that are restarted while requests are still in progress.

---

- There's a node version of this README... here: [README.md](README.md)
- There's a short experiment using slowapp to help diagnose the 'cold-start' problem with the MySQL.Data connector package... here: [aspnet-with-database.md](aspnet-with-database.md).

---

## Build and install on Docker for Mac

```sh
docker build -t slowapp-aspnet:0.0.1 ./aspnet-src
```

Then edit `k8s/aspnet-deployment.yaml` `.spec.template.spec.containers[0].image` to reflect the image tag you gave it, e.g. `slowapp-aspnet:0.0.1`.

Apply the Kubernetes manifests in `k8s` to your Docker for Mac cluster.

```sh
kubectl apply -f k8s/aspnet-deployment.yaml
helm upgrade -i my-ingctl ingress-nginx/ingress-nginx -f k8s/nginx-ingress-values.yaml -n ingress-nginx
helm upgrade -i mysql bitnami/mysql -f k8s/mysql-values.yaml -n dev
```

The app has two endpoints which can be accessed at:

- http://slowapp-aspnet.127.0.0.1.xip.io/slow/{ms}
- http://slowapp-aspnet.127.0.0.1.xip.io/slow/{sec}/db

/slow is delayed by whatever `SLOWAPP_DELAY` is set to in aspnet-deployment.yaml (default is 5000 ms).

We can send multiple requests to the app with [hey](https://github.com/rakyll/hey).

```sh
hey -n 100 http://slowapp-aspnet.127.0.0.1.xip.io/slow/0
```

## Towards zero downtime

We want to record what happens when we send requests for the duration of a rolling restart. Do requests get lost, and how many?

First, we send a request for a slow response with hey, while performing a rolling restart of the pod:

```sh
hey -n 400 -c 100 http://slowapp-aspnet.127.0.0.1.xip.io/slow/5000 & sleep 1 && k rollout restart deploy slowapp-aspnet -n dev

...

Status code distribution:
  [200] 389 responses
  [502] 11 responses
```

Some failures :/

### Adding a readiness probe

We'll add a `readinessProbe` to the deployment. Hopefully this will prevent the termination of old pods before new pods are ready.

```yaml
# added to aspnet-deployment.yaml
readinessProbe:
  httpGet:
    path: /slow/0
    port: http
```

```sh
Status code distribution:
  [200] 393 responses
  [502] 7 responses
```

That's a little better. Still a small number of failures though.

### More replicas

Let's bump the replica count to 3.

```sh
Status code distribution:
  [200] 400 responses
```

Nice, no errors.

### Add a preStop lifecycle hook

What if we use a preStop lifecycle hook? And reset replicas back to 1, and remove the readinessProbe.

This is a synchronous call to a process in the container that must complete before the pod terminates. We'll have it sleep for a short time to ensure all requests are finished.

```yaml
lifecycle:
  preStop:
    exec:
      command: ['/bin/sh', '-c', '/bin/sleep 10']
```

```sh
Status code distribution:
  [200] 400 responses
```

Very nice. All the 200s.

In fact, if we reduce the replicas back to 1, and even remove the readiness probe, we still get 100% success. The lifecycle hook makes a big difference.

#### References:

- https://freecontent.manning.com/handling-client-requests-properly-with-kubernetes/
- https://blog.sebastian-daschner.com/entries/zero-downtime-updates-kubernetes
- https://kubernetes.io/docs/concepts/containers/container-lifecycle-hooks/
- https://kubernetes.io/docs/concepts/workloads/pods/pod/#termination-of-pods

## Bonus features

Slowapp does a couple more things useful for testing error pages and the like.

### Status endpoint

`/status/<status code>` will return a response with that status code, e.g. 404, 500, etc.

### Server error endpoint

You can cause an Internal Error by accessing `/die`.

### Default backend

The nginx ingress values also defines a default backend. We can use slowapp to test its response to various status conditions.
