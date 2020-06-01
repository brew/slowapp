const express = require('express');
const morgan = require('morgan');
const app = express();
const port = 3000;
const delayResponse = process.env['SLOWAPP_DELAY'] || 0;
const hostname = process.env['HOSTNAME'] || 'no hostname';

app.use(morgan('tiny'));

app.get('/slow', (req, res, next) => {
  setTimeout(() => {
    res.send(`Heeeelllllooooo Woooooorld! from ${hostname}`);
  }, delayResponse);
});
app.get('/', (req, res) => res.send(`Hello World! from ${hostname}`));

app.get('/probe/ready', (req, res) => res.send(`Ready`));

app.listen(port, () =>
  console.log(`App listening at http://localhost:${port}`)
);
