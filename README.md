# Rolling a Slow App

This is a demonstrator app (build with node/expressjs) and Kubernetes manifests to deploy it, along with an nginx-ingress controller. The idea is to test what happens to pods that are restarted while requests are still in progress.

---

There's an ASP.NET version of this README... here: [README-ASPNET.md](README-ASPNET.md)

---

## Build and install on Docker for Mac

```sh
docker build -t slowapp:0.0.1 ./node-src
```

Then edit `k8s/node-deployment.yaml` `.spec.template.spec.containers[0].image` to reflect the image tag you gave it, e.g. `slowapp:0.0.1`.

Apply the Kubernetes manifests in `k8s` to your Docker for Mac cluster.

```sh
kubectl apply -f k8s/node-deployment.yaml
helm upgrade -i my-ingctl ingress-nginx/ingress-nginx -f k8s/nginx-ingress-values.yaml -n ingress-nginx
```

The app has two endpoints which can be accessed at:

- http://slowapp.127.0.0.1.xip.io
- http://slowapp.127.0.0.1.xip.io/slow

/slow is delayed by whatever `SLOWAPP_DELAY` is set to in node-deployment.yaml (default is 5000 ms).

We can send multiple requests to the app with [hey](https://github.com/rakyll/hey).

```sh
hey -n 100 http://slowapp.127.0.0.1.xip.io
```

## Towards zero downtime

We want to record what happens when we send requests for the duration of a rolling restart. Do requests get lost, and how many?

First, we send a request for a slow response with hey, while performing a rolling restart of the pod:

```sh
hey -n 400 -c 100 http://slowapp.127.0.0.1.xip.io/slow & sleep 1 \
    && k rollout restart deploy slowapp

...

Status code distribution:
  [502] 400 responses
```

That's not good. They all failed.

### Adding a readiness probe

We'll add a `readinessProbe` to the deployment. Hopefully this will prevent the termination of old pods before new pods are ready.

```yaml
# added to node-deployment.yaml
readinessProbe:
  httpGet:
    path: /probe/ready
    port: http
```

```sh
Status code distribution:
  [200] 100 responses
  [502] 300 responses
```

That's better, but not much. Some of those requests were successful.

### More replicas

Let's bump the replica count to 3.

```sh
Status code distribution:
  [200] 384 responses
  [502] 16 responses
```

Nice, we've really reduced errors now.

### Add a preStop lifecycle hook

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

In fact, if we reduce the replicas back to 1, and even remove the readiness probe, we still get 100% success. The lifecycle hook makes the biggest difference.

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
