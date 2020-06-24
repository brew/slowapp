const http = require('http');
const express = require('express');
const morgan = require('morgan');
const app = express();
const port = 3000;
const delayResponse = process.env['SLOWAPP_DELAY'] || 0;
const hostname = process.env['HOSTNAME'] || 'no hostname';

app.use(morgan('tiny'));

app.all('/', (req, res) => res.send(`Hello World! from ${hostname}`));

app.all('/slow/:ms?', (req, res, next) => {
  const ms = req.params.ms || delayResponse;
  setTimeout(() => {
    res.send(`Heeeelllllooooo Woooooorld! from ${hostname}`);
  }, ms);
});

app.all('/status/:code', (req, res) => {
  res.status(req.params.code)
  res.send(`Slowapp replies with: ${http.STATUS_CODES[req.params.code]}`)
});

app.all('/die', (req, res) => {
  throw { message: 'died' }
})

app.get('/probe/ready', (req, res) => res.send(`Ready`));

app.use((req, res, next) => {
  const message = 'Page not found for slowapp';
  res.status(404).send(message);
});

app.use((err, req, res, next) => {
  console.error(err);
  res.status(500).send('An internal error occurred for slowapp');
});


app.listen(port, () =>
  console.log(`App listening at http://localhost:${port}`)
);
